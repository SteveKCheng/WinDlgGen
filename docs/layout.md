# Layout

How WinDlgGen turns the tree of containers and controls into rectangles.
The rules are those of a much simplified CSS flexbox, with every number a
whole number of dialog units.

## The box model

Each node occupies a **border box**. For a control, this is the rectangle
written to the `.rc` file. For a group box, it is the frame. For a stack, it
is invisible, but its children lie inside it.

- The **margin** surrounds the border box. It is 0 unless given, and adds to
  the container's spacing.
- The **padding** of a container lies inside its border box, between its
  edges and its children. A group box's padding leaves room for its frame
  and caption, and the dialog's padding is its margin.

```
    margin
    +----------------------------- border box ---+
    |  padding                                   |
    |   +----------+  spacing  +----------+      |
    |   |  child   |           |  child   |      |
    |   +----------+           +----------+      |
    |                                            |
    +--------------------------------------------+
```

Gaps between items belong to the container, as its `spacing`, not to the
items, as margins. That way, each gap is set in one place, and two margins
do not add up to twice the intended gap. Margins are for the exceptions.

## Two passes

**Measure**, from the leaves up, gives each node the smallest border box it
needs:

- A control or spacer measures its `width` and `height`, or 0 for either
  that is not given.
- A stack adds up its children along its direction, each with its margin,
  plus its spacing between adjacent children (none next to a spacer).
  Across its direction, it takes its largest child with margin. Then it adds
  its padding, and takes `width` and `height` as minimums.

**Arrange**, from the dialog down, gives each node its rectangle. The dialog
gets its measured size at (0, 0). Each stack then places its children in
order inside its padding:

- **Along the stack**, each child gets its measured size. If the stack is
  larger than its children need, the extra goes to children with `grow`,
  shared by weight. Each share is rounded down, and the last child with
  `grow` takes what rounding leaves, so the stack is filled exactly. With no
  growing children, the extra is left at the end.
- **Across the stack**, the child's `alignSelf`, or else the stack's
  `align`, places it: at the start (left or top), centred, at the end, or
  stretched to the stack's full size. Centring rounds down. A control or
  spacer with its own size in that direction keeps it, so it is placed at
  the start instead of stretched.

Nothing is ever made smaller than its measured size, so a stack always has
room for its children. Controls that end up 0 in either direction are an
error: they had no size of their own and nothing stretched them.

The tab order is the order of the tree, with each group box before its
contents.

## Default metrics

The defaults follow Microsoft's guidelines for the layout of Windows
dialogs:

| What | Dialog units |
|---|---|
| Dialog margin | 7 |
| Gap between the dialog's children (unrelated groups) | 7 |
| Gap between the children of a stack or group (related controls) | 4 |
| Group box padding: top, sides and bottom | 11, 7, 7 |
| Button | 50 × 14 |
| Edit box, combo box (visible part) | height 14 |
| Check box, radio button | height 10 |
| One line of label text | height 8 |

A one-line label (8 high) centred beside an edit box (14 high) is moved 3
down, which lines up the baselines of the two texts.

## What it does not do

- **Measure text.** A label or check box gets its width from the XML, or by
  being stretched. Text too long for its control is clipped without warning.
  Measuring it needs the dialog font's character widths, which a later
  version could take from GDI.
- **Shrink or wrap.** A container larger than its parent's space is not
  reduced; the parent grows instead.
- **Resize at run time.** The output is a fixed layout; the dialog is not
  meant to be resized.
- **Grids.** Columns of labels line up when the labels have the same
  `width`.
