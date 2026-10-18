namespace WinDlgGen;

/// <summary>
/// A node of a dialog's layout tree, as read from the XML.
/// </summary>
/// <remarks>
/// <para>
/// The reader creates the tree, and <see cref="Layout"/> then fills in
/// <see cref="Measured"/> and <see cref="Bounds"/>. Apart from those two,
/// a node does not change after the reader has created it.
/// </para>
/// <para>
/// A node occupies its <em>border box</em>, which is what
/// <see cref="Bounds"/> describes and, for a control or a group box, the
/// rectangle written to the <c>.rc</c> file. Its <see cref="Margin"/>
/// surrounds the border box. A container's <see cref="StackNode.Padding"/>
/// lies inside the border box, around its children.
/// </para>
/// </remarks>
public abstract class Node
{
    /// <summary>Where the node's element is in the XML.</summary>
    public required SourceLocation Location { get; init; }

    /// <summary>Space around the border box, added to the container's spacing.</summary>
    public Thickness Margin { get; init; }

    /// <summary>
    /// The width of the border box. For a control or spacer, this is a
    /// fixed width, and null means it has none of its own: its measured
    /// width is 0 and it is meant to be stretched. For a container, it is
    /// a minimum; null means no minimum.
    /// </summary>
    public int? Width { get; init; }

    /// <summary>The height of the border box, with the same meaning as <see cref="Width"/>.</summary>
    public int? Height { get; init; }

    /// <summary>
    /// Weight for sharing out the space a stack has beyond what its
    /// children need, along the stack's direction. 0 means the node never
    /// grows.
    /// </summary>
    public int Grow { get; init; }

    /// <summary>
    /// The node's own placement across its container, overriding the
    /// container's <see cref="StackNode.Align"/>; null to use that.
    /// </summary>
    public Alignment? AlignSelf { get; init; }

    /// <summary>
    /// The smallest border box the node needs, set by the measure pass of
    /// <see cref="Layout"/>. Never smaller than <see cref="Width"/> and
    /// <see cref="Height"/>, where those are given.
    /// </summary>
    public Size Measured { get; internal set; }

    /// <summary>
    /// The border box relative to the dialog's top-left corner, set by the
    /// arrange pass of <see cref="Layout"/>. At least as large as
    /// <see cref="Measured"/> in both directions.
    /// </summary>
    public Rect Bounds { get; internal set; }

    /// <summary>
    /// Whether the node keeps its own size along <paramref name="o"/> when
    /// its container would stretch it: true for a control or spacer with
    /// an explicit size in that direction.
    /// </summary>
    public virtual bool HasFixedSize(Orientation o) => false;
}

/// <summary>
/// Empty space in a stack. It writes nothing to the output, and replaces
/// the stack's spacing on both of its sides.
/// </summary>
public sealed class SpacerNode : Node
{
    public override bool HasFixedSize(Orientation o)
        => (o == Orientation.Vertical ? Height : Width) is not null;
}

/// <summary>The kinds of control the XML can describe.</summary>
public enum ControlKind
{
    Label,
    Button,
    CheckBox,
    Radio,
    Edit,
    ComboBox,
    ListBox,
    Trackbar,
    GroupBox,

    /// <summary>A control of any window class, given by the XML.</summary>
    Custom,
}

/// <summary>
/// Everything that is written for one <c>CONTROL</c> statement apart from
/// its rectangle and the flags that depend on its place in the tab order
/// (<c>WS_GROUP</c> and <c>WS_TABSTOP</c>), which <see cref="RcWriter"/>
/// works out.
/// </summary>
public sealed class ControlSpec
{
    public required ControlKind Kind { get; init; }

    /// <summary>
    /// The control's identifier: a C identifier such as <c>IDC_SCALE</c>,
    /// which the <c>.rc</c> file must have defined elsewhere, or a decimal
    /// integer. <c>IDC_STATIC</c> for controls that the program never
    /// refers to.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>The window text, unescaped. May be empty.</summary>
    public string Text { get; init; } = "";

    /// <summary>The window class name, such as <c>Button</c>.</summary>
    public required string ClassName { get; init; }

    /// <summary>
    /// Style flags other than <c>WS_GROUP</c>, <c>WS_TABSTOP</c>,
    /// <c>WS_DISABLED</c> and <c>WS_VISIBLE</c>, in the order to write
    /// them. Each item is a symbol, a number, or an expression the reader
    /// has checked to contain only symbols, numbers, <c>|</c> and
    /// parentheses.
    /// </summary>
    public required IReadOnlyList<string> Style { get; init; }

    /// <summary>Extended style expression, checked like <see cref="Style"/>; null for none.</summary>
    public string? ExStyle { get; init; }

    /// <summary>Explicit <c>WS_TABSTOP</c> setting; null for the default by kind and position.</summary>
    public bool? TabStop { get; init; }

    /// <summary>Explicit <c>WS_GROUP</c> setting; null for the default by kind and position.</summary>
    public bool? Group { get; init; }

    /// <summary>False to create the control hidden. A hidden control still takes up its space.</summary>
    public bool Visible { get; init; } = true;

    /// <summary>False to create the control disabled.</summary>
    public bool Enabled { get; init; } = true;
}

/// <summary>A control: a leaf of the layout tree.</summary>
public sealed class ControlNode : Node
{
    public required ControlSpec Spec { get; init; }

    /// <summary>
    /// Added to the height of <see cref="Node.Bounds"/> when writing the
    /// control, without taking up space in the layout. For a drop-down
    /// combo box, whose height in a dialog template includes its open list.
    /// </summary>
    public int ExtraHeight { get; init; }

    public override bool HasFixedSize(Orientation o)
        => (o == Orientation.Vertical ? Height : Width) is not null;
}

/// <summary>
/// A container that places its children one after another along
/// <see cref="Orientation"/>.
/// </summary>
public class StackNode : Node
{
    public required Orientation Orientation { get; init; }

    /// <summary>
    /// The gap between two adjacent children, unless one of them is a
    /// <see cref="SpacerNode"/>.
    /// </summary>
    public int Spacing { get; init; }

    /// <summary>Space between the border box and the children.</summary>
    public Thickness Padding { get; init; }

    /// <summary>How children are placed across the stack, unless they set <see cref="Node.AlignSelf"/>.</summary>
    public Alignment Align { get; init; }

    /// <summary>The children, in layout order, which is also the tab order.</summary>
    public List<Node> Children { get; } = [];
}

/// <summary>
/// A group box: a vertical stack drawn with a frame and a caption. The
/// frame is the stack's border box, and its padding leaves room for them.
/// </summary>
public sealed class GroupNode : StackNode
{
    /// <summary>The group box control, written before the children.</summary>
    public required ControlSpec Box { get; init; }
}

/// <summary>
/// A dialog: the root of a layout tree, laid out as a vertical stack whose
/// padding is the dialog's margin.
/// </summary>
public sealed class DialogNode : StackNode
{
    /// <summary>The resource identifier, as for <see cref="ControlSpec.Id"/>.</summary>
    public required string Id { get; init; }

    /// <summary>The title bar text; null to write no <c>CAPTION</c>.</summary>
    public string? Caption { get; init; }

    public required int FontSize { get; init; }
    public required string FontFace { get; init; }

    /// <summary>The flags of the <c>STYLE</c> statement, checked as for <see cref="ControlSpec.Style"/>.</summary>
    public required IReadOnlyList<string> Style { get; init; }

    /// <summary>The <c>EXSTYLE</c> expression; null to write none.</summary>
    public string? ExStyle { get; init; }
}
