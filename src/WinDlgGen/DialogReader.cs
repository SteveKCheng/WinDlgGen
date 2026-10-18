using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;

namespace WinDlgGen;

/// <summary>
/// Reads a dialog description from XML into a layout tree.
/// </summary>
/// <remarks>
/// The reader is strict: an unknown element or attribute, or a value of
/// the wrong form, is an error, so that a misspelling is reported rather
/// than silently ignored. See <c>docs/format.md</c> for the format.
/// </remarks>
public static partial class DialogReader
{
    /// <summary>
    /// Reads the XML file at <paramref name="path"/>.
    /// </summary>
    /// <param name="path">The file to read.</param>
    /// <param name="reportedName">The file name to use in diagnostics.</param>
    /// <param name="diagnostics">Receives the problems found.</param>
    /// <returns>
    /// The dialog, or null if the file could not be parsed at all. When the
    /// dialog is returned with errors reported, it is still a well-formed
    /// tree, but its contents should not be written out.
    /// </returns>
    public static DialogNode? ReadFile(string path, string reportedName, DiagnosticBag diagnostics)
    {
        using var stream = File.OpenRead(path);
        return Read(stream, reportedName, diagnostics);
    }

    /// <summary>
    /// Reads XML from <paramref name="stream"/>; otherwise as
    /// <see cref="ReadFile"/>.
    /// </summary>
    public static DialogNode? Read(Stream stream, string reportedName, DiagnosticBag diagnostics)
    {
        // DTDs are refused, so that the input cannot pull in other files
        // or expand entities without limit.
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = true,
            IgnoreProcessingInstructions = true,
        };

        XDocument document;
        try
        {
            using var reader = XmlReader.Create(stream, settings);
            document = XDocument.Load(reader, LoadOptions.SetLineInfo);
        }
        catch (XmlException e)
        {
            diagnostics.Error(DiagnosticCodes.XmlSyntax,
                new SourceLocation(reportedName, e.LineNumber, e.LinePosition), e.Message);
            return null;
        }

        var context = new Context(reportedName, diagnostics);
        var root = document.Root!;
        if (root.Name != "dialog")
        {
            diagnostics.Error(DiagnosticCodes.UnknownElement, context.Locate(root),
                $"The root element must be <dialog>, not <{root.Name}>");
            return null;
        }

        return context.ReadDialog(root);
    }

    // Default metrics, in dialog units. They follow the spacing that
    // Microsoft's guidelines for Windows dialogs recommend.

    /// <summary>Space between the dialog's edges and its contents.</summary>
    private const int DialogMargin = 7;

    /// <summary>Gap between unrelated items: the children of the dialog itself.</summary>
    private const int UnrelatedSpacing = 7;

    /// <summary>Gap between related items: the children of stacks and group boxes.</summary>
    private const int RelatedSpacing = 4;

    /// <summary>
    /// Space inside a group box's frame. The top is larger to clear the
    /// caption, which is drawn across the frame's top edge.
    /// </summary>
    private static readonly Thickness GroupPadding = new(11, 7, 7, 7);

    /// <summary>The height of a drop-down combo box's open list, by default.</summary>
    private const int DefaultListHeight = 80;

    /// <summary>The usual pattern for identifiers and numbers in style expressions and IDs.</summary>
    [GeneratedRegex(@"^(?:[A-Za-z_][A-Za-z0-9_]*|-?[0-9]+)$")]
    private static partial Regex IdPattern();

    /// <summary>
    /// A style expression: symbols and numbers joined by <c>|</c>, with
    /// optional parentheses. <c>NOT</c> is refused; see
    /// <see cref="Attributes.Style"/>.
    /// </summary>
    [GeneratedRegex(@"^[A-Za-z0-9_|()\s]+$")]
    private static partial Regex StylePattern();

    [GeneratedRegex(@"\bNOT\b")]
    private static partial Regex NotPattern();

    /// <summary>
    /// The state of reading one file: where to report problems, and the
    /// identifiers seen so far.
    /// </summary>
    private sealed class Context(string fileName, DiagnosticBag diagnostics)
    {
        /// <summary>Control identifiers used so far, other than IDC_STATIC.</summary>
        private readonly Dictionary<string, SourceLocation> _ids = [];

        public SourceLocation Locate(XObject x)
            => x is IXmlLineInfo info && info.HasLineInfo()
                ? new SourceLocation(fileName, info.LineNumber, info.LinePosition)
                : new SourceLocation(fileName, 0, 0);

        public DialogNode ReadDialog(XElement e)
        {
            var a = new Attributes(this, e);
            var id = a.Id("id", required: true) ?? "0";
            var caption = a.Text("caption");
            int fontSize = a.Int("fontSize") ?? 8;
            var fontFace = a.Text("fontFace") ?? "MS Shell Dlg 2";
            bool child = a.Choice("kind", ["modal", "child"]) == "child";
            bool center = a.Bool("center") ?? !child;
            var frame = a.Style("style");
            var exStyle = a.Style("exstyle");

            if (fontFace.Length == 0)
                Error(DiagnosticCodes.BadValue, e, "The fontFace attribute must not be empty");

            // DS_SETFONT is required for the FONT statement to take effect.
            List<string> style = ["DS_SETFONT"];
            if (center)
                style.Add("DS_CENTER");
            if (frame is not null)
                style.Add(frame);
            else if (child)
                style.AddRange(["DS_CONTROL", "WS_CHILD"]);
            else
                style.AddRange(["DS_MODALFRAME", "WS_POPUP", "WS_CAPTION", "WS_SYSMENU"]);

            var dialog = new DialogNode
            {
                Location = Locate(e),
                Id = id,
                Caption = caption,
                FontSize = fontSize,
                FontFace = fontFace,
                Style = style,
                ExStyle = exStyle,
                Orientation = Orientation.Vertical,
                Spacing = a.Int("spacing") ?? UnrelatedSpacing,
                Padding = a.Thickness("padding") ?? Thickness.Uniform(DialogMargin),
                Align = a.Alignment("align") ?? Alignment.Stretch,
                Width = a.Int("width"),
                Height = a.Int("height"),
            };
            a.Finish();

            ReadChildren(e, dialog);
            return dialog;
        }

        private void ReadChildren(XElement e, StackNode parent)
        {
            foreach (var node in e.Nodes())
            {
                if (node is XText text && !string.IsNullOrWhiteSpace(text.Value))
                {
                    Error(DiagnosticCodes.BadText, node,
                        "Text is not allowed between elements; put a control's text in its text attribute");
                }
                else if (node is XElement child && ReadNode(child) is { } n)
                {
                    parent.Children.Add(n);
                }
            }
        }

        /// <summary>
        /// Reads a child of a container; null after reporting an unknown element.
        /// </summary>
        private Node? ReadNode(XElement e)
        {
            if (e.Name.NamespaceName.Length == 0)
            {
                switch (e.Name.LocalName)
                {
                    case "vstack": return ReadStack(e, Orientation.Vertical);
                    case "hstack": return ReadStack(e, Orientation.Horizontal);
                    case "group": return ReadGroup(e);
                    case "spacer": return ReadSpacer(e);
                }

                if (ReadControl(e) is { } control)
                    return control;
            }

            Error(DiagnosticCodes.UnknownElement, e, $"Unknown element <{e.Name}>");
            return null;
        }

        private StackNode ReadStack(XElement e, Orientation o)
        {
            var a = new Attributes(this, e);
            var stack = new StackNode
            {
                Location = Locate(e),
                Orientation = o,
                Spacing = a.Int("spacing") ?? RelatedSpacing,
                Padding = a.Thickness("padding") ?? Thickness.Zero,
                // A horizontal stack centres by default, which lines up the
                // text of a one-line label with that of the control beside it.
                Align = a.Alignment("align")
                        ?? (o == Orientation.Vertical ? Alignment.Stretch : Alignment.Center),
                Margin = a.Thickness("margin") ?? Thickness.Zero,
                Width = a.Int("width"),
                Height = a.Int("height"),
                Grow = a.Int("grow") ?? 0,
                AlignSelf = a.Alignment("alignSelf"),
            };
            a.Finish();
            ReadChildren(e, stack);
            return stack;
        }

        private GroupNode ReadGroup(XElement e)
        {
            var a = new Attributes(this, e);
            var box = ReadSpec(a, ControlKind.GroupBox, "Button", ["BS_GROUPBOX"], idRequired: false);
            var group = new GroupNode
            {
                Location = Locate(e),
                Box = box,
                Orientation = Orientation.Vertical,
                Spacing = a.Int("spacing") ?? RelatedSpacing,
                Padding = a.Thickness("padding") ?? GroupPadding,
                Align = a.Alignment("align") ?? Alignment.Stretch,
                Margin = a.Thickness("margin") ?? Thickness.Zero,
                Width = a.Int("width"),
                Height = a.Int("height"),
                Grow = a.Int("grow") ?? 0,
                AlignSelf = a.Alignment("alignSelf"),
            };
            a.Finish();
            ReadChildren(e, group);
            return group;
        }

        private SpacerNode ReadSpacer(XElement e)
        {
            var a = new Attributes(this, e);
            var spacer = new SpacerNode
            {
                Location = Locate(e),
                Width = a.Int("width"),
                Height = a.Int("height"),
                Grow = a.Int("grow") ?? 0,
            };
            a.Finish();
            NoChildren(e);
            return spacer;
        }

        /// <summary>
        /// Reads a control element; null if <paramref name="e"/> is not one.
        /// </summary>
        private ControlNode? ReadControl(XElement e)
        {
            var a = new Attributes(this, e);
            ControlSpec spec;
            int? defaultWidth = null, defaultHeight = null;
            int extraHeight = 0;

            switch (e.Name.LocalName)
            {
                case "label":
                {
                    List<string> style = [a.Choice("textAlign", ["left", "center", "right"]) switch
                    {
                        "center" => "SS_CENTER",
                        "right" => "SS_RIGHT",
                        _ => "SS_LEFT",
                    }];
                    if (a.Bool("noPrefix") == true)
                        style.Add("SS_NOPREFIX");
                    spec = ReadSpec(a, ControlKind.Label, "Static", style, idRequired: false);
                    defaultHeight = 8;
                    break;
                }

                case "button":
                    spec = ReadSpec(a, ControlKind.Button, "Button",
                        [a.Bool("default") == true ? "BS_DEFPUSHBUTTON" : "BS_PUSHBUTTON"],
                        idRequired: true);
                    defaultWidth = 50;
                    defaultHeight = 14;
                    break;

                case "checkbox":
                    spec = ReadSpec(a, ControlKind.CheckBox, "Button",
                        [a.Bool("threeState") == true ? "BS_AUTO3STATE" : "BS_AUTOCHECKBOX"],
                        idRequired: true);
                    defaultHeight = 10;
                    break;

                case "radio":
                    spec = ReadSpec(a, ControlKind.Radio, "Button", ["BS_AUTORADIOBUTTON"], idRequired: true);
                    defaultHeight = 10;
                    break;

                case "edit":
                {
                    List<string> style = ["ES_LEFT"];
                    if (a.Bool("multiline") == true)
                        style.AddRange(["ES_MULTILINE", "ES_AUTOVSCROLL", "ES_WANTRETURN", "WS_VSCROLL"]);
                    else
                        style.Add("ES_AUTOHSCROLL");
                    if (a.Bool("password") == true)
                        style.Add("ES_PASSWORD");
                    if (a.Bool("number") == true)
                        style.Add("ES_NUMBER");
                    if (a.Bool("readOnly") == true)
                        style.Add("ES_READONLY");
                    style.Add("WS_BORDER");
                    spec = ReadSpec(a, ControlKind.Edit, "Edit", style, idRequired: true);
                    defaultHeight = 14;
                    break;
                }

                case "combobox":
                {
                    List<string> style = [a.Bool("editable") == true ? "CBS_DROPDOWN | CBS_AUTOHSCROLL" : "CBS_DROPDOWNLIST"];
                    if (a.Bool("sort") == true)
                        style.Add("CBS_SORT");
                    style.Add("WS_VSCROLL");
                    extraHeight = a.Int("listHeight") ?? DefaultListHeight;
                    spec = ReadSpec(a, ControlKind.ComboBox, "ComboBox", style, idRequired: true);
                    defaultHeight = 14;
                    break;
                }

                case "listbox":
                {
                    // LBS_NOINTEGRALHEIGHT keeps the list box at the height
                    // the layout gives it, rather than rounding it down to a
                    // whole number of items.
                    List<string> style = ["LBS_NOTIFY", "LBS_NOINTEGRALHEIGHT"];
                    if (a.Bool("sort") == true)
                        style.Add("LBS_SORT");
                    style.AddRange(["WS_VSCROLL", "WS_BORDER"]);
                    spec = ReadSpec(a, ControlKind.ListBox, "ListBox", style, idRequired: true);
                    break;
                }

                case "trackbar":
                {
                    List<string> style = ["TBS_HORZ"];
                    if (a.Bool("ticks") == true)
                        style.Add("TBS_AUTOTICKS");
                    spec = ReadSpec(a, ControlKind.Trackbar, "msctls_trackbar32", style, idRequired: true);
                    defaultHeight = 15;
                    break;
                }

                case "control":
                {
                    var className = a.Text("class", required: true) ?? "Static";
                    if (className.Length == 0)
                        Error(DiagnosticCodes.BadValue, e, "The class attribute must not be empty");
                    spec = ReadSpec(a, ControlKind.Custom, className, [], idRequired: false);
                    break;
                }

                default:
                    return null;
            }

            var control = new ControlNode
            {
                Location = Locate(e),
                Spec = spec,
                ExtraHeight = extraHeight,
                Margin = a.Thickness("margin") ?? Thickness.Zero,
                Width = a.Int("width") ?? defaultWidth,
                Height = a.Int("height") ?? defaultHeight,
                Grow = a.Int("grow") ?? 0,
                AlignSelf = a.Alignment("alignSelf"),
            };
            a.Finish();
            NoChildren(e);
            return control;
        }

        /// <summary>
        /// Reads the attributes that every control has, and records its
        /// identifier.
        /// </summary>
        /// <param name="a">The element's attributes.</param>
        /// <param name="kind">The kind of control.</param>
        /// <param name="className">The window class.</param>
        /// <param name="style">The flags for the kind of control and its
        /// attributes; the <c>style</c> attribute is appended to them.</param>
        /// <param name="idRequired">Whether the control is useless without
        /// an identifier, as an interactive control is. Otherwise the
        /// identifier defaults to <c>IDC_STATIC</c>.</param>
        private ControlSpec ReadSpec(Attributes a, ControlKind kind, string className,
                                     List<string> style, bool idRequired)
        {
            var id = a.Id("id", required: idRequired) ?? "IDC_STATIC";
            if (a.Style("style") is { } extra)
                style.Add(extra);

            return new ControlSpec
            {
                Kind = kind,
                Id = id,
                ClassName = className,
                Text = a.Text("text") ?? "",
                Style = style,
                ExStyle = a.Style("exstyle"),
                TabStop = a.Bool("tabstop"),
                Group = a.Bool("group"),
                Visible = a.Bool("visible") ?? true,
                Enabled = a.Bool("enabled") ?? true,
            };
        }

        /// <summary>
        /// Records a control or dialog identifier, reporting it if it is
        /// already used in this file.
        /// </summary>
        public void UseId(string id, XAttribute where)
        {
            if (id == "IDC_STATIC")
                return;

            var location = Locate(where);
            if (_ids.TryGetValue(id, out var previous))
            {
                diagnostics.Error(DiagnosticCodes.DuplicateId, location,
                    $"The identifier {id} is already used at line {previous.Line}");
            }
            else
            {
                _ids.Add(id, location);
            }
        }

        private void NoChildren(XElement e)
        {
            if (e.Nodes().FirstOrDefault(n => n is XElement || n is XText t && !string.IsNullOrWhiteSpace(t.Value))
                is { } first)
            {
                Error(DiagnosticCodes.BadText, first,
                    $"<{e.Name}> cannot have content; put a control's text in its text attribute");
            }
        }

        public void Error(string code, XObject where, string message)
            => diagnostics.Error(code, Locate(where), message);
    }

    /// <summary>
    /// Takes the attributes of one element, converting and checking each,
    /// and reports at the end any that were not taken.
    /// </summary>
    /// <remarks>
    /// Every getter returns null if the attribute is absent, and also
    /// after reporting a bad value, so that reading can go on and find
    /// further problems. Call <see cref="Finish"/> once all attributes the
    /// element may have are taken.
    /// </remarks>
    private sealed class Attributes(Context context, XElement element)
    {
        private readonly HashSet<string> _taken = [];

        private XAttribute? Take(string name, bool required)
        {
            _taken.Add(name);
            var attribute = element.Attribute(name);
            if (attribute is null && required)
            {
                context.Error(DiagnosticCodes.MissingAttribute, element,
                    $"<{element.Name}> needs a {name} attribute");
            }
            return attribute;
        }

        private void Bad(XAttribute attribute, string expected)
            => context.Error(DiagnosticCodes.BadValue, attribute,
                $"The {attribute.Name} attribute must be {expected}, not \"{attribute.Value}\"");

        /// <summary>
        /// Text for the output, which may hold any characters but control
        /// characters other than newline and tab.
        /// </summary>
        public string? Text(string name, bool required = false)
        {
            if (Take(name, required) is not { } attribute)
                return null;
            if (attribute.Value.Any(c => char.IsControl(c) && c != '\n' && c != '\t'))
            {
                Bad(attribute, "text without control characters other than newline and tab");
                return null;
            }
            return attribute.Value;
        }

        /// <summary>A non-negative number of dialog units, or another small count.</summary>
        public int? Int(string name)
        {
            if (Take(name, required: false) is not { } attribute)
                return null;
            if (!Units.TryParse(attribute.Value, out int value))
            {
                Bad(attribute, $"a whole number from 0 to {Units.Max}");
                return null;
            }
            return value;
        }

        public bool? Bool(string name)
        {
            if (Take(name, required: false) is not { } attribute)
                return null;
            switch (attribute.Value)
            {
                case "true": return true;
                case "false": return false;
                default: Bad(attribute, "true or false"); return null;
            }
        }

        /// <summary>One of <paramref name="choices"/>.</summary>
        public string? Choice(string name, string[] choices)
        {
            if (Take(name, required: false) is not { } attribute)
                return null;
            if (!choices.Contains(attribute.Value))
            {
                Bad(attribute, string.Join(", ", choices[..^1]) + " or " + choices[^1]);
                return null;
            }
            return attribute.Value;
        }

        public WinDlgGen.Alignment? Alignment(string name)
            => Choice(name, ["start", "center", "end", "stretch"]) switch
            {
                "start" => WinDlgGen.Alignment.Start,
                "center" => WinDlgGen.Alignment.Center,
                "end" => WinDlgGen.Alignment.End,
                "stretch" => WinDlgGen.Alignment.Stretch,
                _ => null,
            };

        public WinDlgGen.Thickness? Thickness(string name)
        {
            if (Take(name, required: false) is not { } attribute)
                return null;
            if (!WinDlgGen.Thickness.TryParse(attribute.Value, out var value))
            {
                Bad(attribute, "one to four whole numbers separated by spaces");
                return null;
            }
            return value;
        }

        /// <summary>
        /// An identifier: a C identifier or a decimal integer. Checked to
        /// be unique within the file.
        /// </summary>
        public string? Id(string name, bool required)
        {
            if (Take(name, required) is not { } attribute)
                return null;
            if (!IdPattern().IsMatch(attribute.Value))
            {
                Bad(attribute, "a C identifier or a whole number");
                return null;
            }
            context.UseId(attribute.Value, attribute);
            return attribute.Value;
        }

        /// <summary>
        /// A style expression, written to the output as it is, so it is
        /// checked to hold nothing that could break the <c>.rc</c> syntax.
        /// </summary>
        /// <remarks>
        /// <c>NOT</c> is refused: what it removes depends on where it is in
        /// the expression and on rc's defaults, which is hard to predict.
        /// The attributes <c>tabstop</c>, <c>group</c>, <c>visible</c> and
        /// <c>enabled</c>, or a dialog's <c>style</c>, cover its uses.
        /// </remarks>
        public string? Style(string name)
        {
            if (Take(name, required: false) is not { } attribute)
                return null;
            var value = string.Join(' ', attribute.Value.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            if (value.Length == 0 || !StylePattern().IsMatch(value) || NotPattern().IsMatch(value)
                || value.StartsWith('|') || value.EndsWith('|'))
            {
                Bad(attribute, "style flags joined by | (without NOT)");
                return null;
            }
            return value;
        }

        /// <summary>Reports every attribute that no getter took.</summary>
        public void Finish()
        {
            foreach (var attribute in element.Attributes())
            {
                if (attribute.IsNamespaceDeclaration || _taken.Contains(attribute.Name.ToString()))
                    continue;
                context.Error(DiagnosticCodes.UnknownAttribute, attribute,
                    $"<{element.Name}> has no attribute {attribute.Name}");
            }
        }
    }
}
