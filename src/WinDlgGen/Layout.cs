namespace WinDlgGen;

/// <summary>
/// Computes the position and size of every node of a dialog, in two
/// passes: measuring bottom-up, then arranging top-down.
/// </summary>
/// <remarks>
/// See <c>docs/layout.md</c> for the rules. Nothing is ever made smaller
/// than its measured size, so the arrange pass cannot run out of space.
/// </remarks>
public static class Layout
{
    /// <summary>
    /// Lays out <paramref name="dialog"/>, setting <see cref="Node.Measured"/>
    /// and <see cref="Node.Bounds"/> on every node.
    /// </summary>
    /// <remarks>
    /// Postconditions: the dialog's bounds are at (0, 0) with its measured
    /// size; every node's bounds are at least its measured size; and
    /// errors are reported for controls left with no width or height and
    /// for a dialog too large for a template. The bounds are set even when
    /// errors are reported.
    /// </remarks>
    public static void Run(DialogNode dialog, DiagnosticBag diagnostics)
    {
        Measure(dialog);
        Arrange(dialog, new Rect(0, 0, dialog.Measured.Width, dialog.Measured.Height));

        if (dialog.Measured.Width > Units.Max || dialog.Measured.Height > Units.Max)
        {
            diagnostics.Error(DiagnosticCodes.TooLarge, dialog.Location,
                $"The dialog is {dialog.Measured.Width} by {dialog.Measured.Height} dialog units, "
                + $"more than the {Units.Max} a dialog template can hold");
        }

        CheckSizes(dialog, diagnostics);
    }

    /// <summary>
    /// Sets <see cref="Node.Measured"/> on <paramref name="node"/> and all
    /// its descendants.
    /// </summary>
    private static void Measure(Node node)
    {
        if (node is not StackNode stack)
        {
            node.Measured = new Size(node.Width ?? 0, node.Height ?? 0);
            return;
        }

        var o = stack.Orientation;
        int along = 0, across = 0;
        for (int i = 0; i < stack.Children.Count; i++)
        {
            var child = stack.Children[i];
            Measure(child);
            along += GapBefore(stack, i) + child.Measured.Along(o) + child.Margin.Along(o);
            across = Math.Max(across, child.Measured.Across(o) + child.Margin.Across(o));
        }

        var content = Size.FromAxes(o, along, across);
        stack.Measured = new Size(
            Math.Max(content.Width + stack.Padding.Horizontal, stack.Width ?? 0),
            Math.Max(content.Height + stack.Padding.Vertical, stack.Height ?? 0));
    }

    /// <summary>
    /// The gap the stack leaves before its child at <paramref name="index"/>:
    /// its spacing, except before the first child and next to a spacer.
    /// </summary>
    private static int GapBefore(StackNode stack, int index)
        => index > 0
           && stack.Children[index - 1] is not SpacerNode
           && stack.Children[index] is not SpacerNode
            ? stack.Spacing
            : 0;

    /// <summary>
    /// Places <paramref name="node"/> at <paramref name="bounds"/> and
    /// arranges its descendants inside.
    /// </summary>
    /// <remarks>
    /// Preconditions: <paramref name="node"/> has been measured, and
    /// <paramref name="bounds"/> is at least its measured size.
    /// </remarks>
    private static void Arrange(Node node, Rect bounds)
    {
        node.Bounds = bounds;
        if (node is not StackNode stack)
            return;

        var o = stack.Orientation;
        var content = bounds.Deflate(stack.Padding);
        int contentAlong = content.Size.Along(o);
        int contentAcross = content.Size.Across(o);

        // The space beyond what the children need. It is never negative,
        // since bounds is at least the measured size, which includes the
        // children, gaps and padding.
        int needed = 0, totalGrow = 0, lastGrower = -1;
        for (int i = 0; i < stack.Children.Count; i++)
        {
            var child = stack.Children[i];
            needed += GapBefore(stack, i) + child.Measured.Along(o) + child.Margin.Along(o);
            if (child.Grow > 0)
            {
                totalGrow += child.Grow;
                lastGrower = i;
            }
        }
        int extra = contentAlong - needed;
        int extraLeft = extra;

        int pos = o == Orientation.Vertical ? content.Y : content.X;
        int acrossStart = o == Orientation.Vertical ? content.X : content.Y;

        for (int i = 0; i < stack.Children.Count; i++)
        {
            var child = stack.Children[i];
            pos += GapBefore(stack, i);

            // Share out the extra space by weight, rounding down; the last
            // grower takes what rounding leaves, so that the stack is
            // filled exactly.
            int share = 0;
            if (child.Grow > 0)
            {
                share = i == lastGrower ? extraLeft : (int)((long)extra * child.Grow / totalGrow);
                extraLeft -= share;
            }
            int along = child.Measured.Along(o) + share;

            int room = contentAcross - child.Margin.Across(o);
            var align = child.AlignSelf ?? stack.Align;
            if (align == Alignment.Stretch && child.HasFixedSize(Flip(o)))
                align = Alignment.Start;

            int across = align == Alignment.Stretch ? room : child.Measured.Across(o);
            int offset = align switch
            {
                Alignment.Center => (room - across) / 2,
                Alignment.End => room - across,
                _ => 0,
            };

            Arrange(child, Rect.FromAxes(o,
                pos + child.Margin.StartAlong(o),
                acrossStart + child.Margin.StartAcross(o) + offset,
                along, across));

            pos += child.Margin.Along(o) + along;
        }
    }

    private static Orientation Flip(Orientation o)
        => o == Orientation.Vertical ? Orientation.Horizontal : Orientation.Vertical;

    /// <summary>
    /// Reports controls that ended up with no width or height: they have
    /// no size of their own and were not stretched.
    /// </summary>
    private static void CheckSizes(Node node, DiagnosticBag diagnostics)
    {
        if (node is ControlNode control)
        {
            if (control.Bounds.Width == 0 || control.Bounds.Height == 0)
            {
                string which = control.Bounds.Width == 0 ? "width" : "height";
                diagnostics.Error(DiagnosticCodes.ZeroSize, control.Location,
                    $"The control has no {which}: give it a {which} attribute, "
                    + "or place it where its container stretches it");
            }
        }
        else if (node is StackNode stack)
        {
            foreach (var child in stack.Children)
                CheckSizes(child, diagnostics);
        }
    }
}
