# Layout

How WinDlgGen turns the tree of containers and controls into rectangles.
The rules are those of a much simplified CSS flexbox, with every number a
whole number of dialog units.

## The box model

Each node occupies a **border box**. For a control, this is the rectangle
written to the `.rc` file. For a group box, it is the frame. For a stack, it
is invisible, but its children lie inside it.

- The **margin** surrounds the border box: extra space on each side of the
  node, 0 unless given.
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

### Spacing and margins

The gaps between a container's children belong to the container, as its
`spacing`, not to the children. Each gap is therefore set in one place, and
the defaults (see below) give the usual gaps without any attributes.

**Margins do not collapse; they add.** In CSS, the gaps between blocks are
made by their margins, so two adjacent vertical margins collapse into the
larger of the two, rather than adding up to twice the intended gap. Here
the container makes the gaps, so a margin is simply extra space. Between
two adjacent children of a stack, the gap is

```
    (trailing margin of the first) + spacing + (leading margin of the second)
```

where the trailing margin is the bottom one in a vertical stack and the
right one in a horizontal stack, and the leading margin the top or left
one. A margin at the edge of a stack adds to the stack's padding in the
same way, and a margin counts in the size of the stack.

Margins are therefore for exceptions: indenting a control under the check
box that enables it (`margin="0 0 0 10"`), or a little more room above one
item.

## Notation

The formulas below are for a stack whose direction is vertical or
horizontal:

- For a size or a pair of margins or paddings, *along* is its extent in
  the stack's direction, and *across* its extent in the other. In a
  vertical stack, *along* is height, or top + bottom; *across* is width, or
  left + right.
- The stack has padding `P`, spacing `s` and children `c[1]` to `c[n]`.
- `m(c)` is a child's measured size and `M(c)` its margin.
- `gap[i]`, for `i` from 2 to `n`, is the gap the stack leaves before
  `c[i]`: `s`, or 0 if `c[i-1]` or `c[i]` is a spacer.
- `outer(c) = along(m(c)) + along(M(c))` is the space a child takes along
  the stack, margins included.

## Measure

From the leaves up, each node gets the smallest border box it needs:

```
    control or spacer:
        m = (width or 0, height or 0)

    stack:
        along  = along(P)  + sum of outer(c[i])  + sum of gap[i]
        across = across(P) + max of (across(m(c[i])) + across(M(c[i])))
                                                    (0 if there are no children)
        then raise the width and height to the stack's width and height
        attributes, if given
```

## Arrange

From the dialog down, each node gets its rectangle. The dialog gets its
measured size at (0, 0). Each stack with border box `B` then places its
children inside its content box `C`, which is `B` less the padding `P`.

**Along the stack**, each child gets its measured size plus a share of the
extra space:

```
    extra    = along(C) - (sum of outer(c[i]) + sum of gap[i])
    G        = sum of grow(c[i])
    share[i] = 0                                  if grow(c[i]) = 0
             = floor(extra * grow(c[i]) / G)      for each growing child but the last
             = extra - (the other shares)         for the last growing child
    size along of c[i] = along(m(c[i])) + share[i]
```

The last growing child takes what rounding leaves, so the stack is filled
exactly. With no growing children, the extra space is left at the end. The
children are placed in order from the start of `C`. Each one's border box
starts after the previous child's border box and trailing margin, then the
gap, then its own leading margin.

**Across the stack**, the child's `alignSelf`, or else the stack's `align`,
places it in the room its margins leave:

```
    room[i] = across(C) - across(M(c[i]))
    size    = room[i]                       for stretch
            = across(m(c[i]))               otherwise
    offset  = 0                             for start (left or top)
            = floor((room[i] - size) / 2)   for center
            = room[i] - size                for end
```

The offset is from the start of `C` plus the child's leading margin. A
control or spacer with its own size across the stack keeps it, so
`stretch` places it as `start`.

Since every border box is at least the node's measured size, `extra` and
`room[i] - across(m(c[i]))` are never negative: nothing is ever made smaller
than its measured size, so a stack always has room for its children.
Controls that end up 0 in either direction are an error: they had no size
of their own and nothing stretched them.

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
down, which lines up the two texts. Centring does not line up text in every
case; see [Planned: aligning text baselines](#planned-aligning-text-baselines).

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
- **Align baselines.** See the next section.

## Planned: aligning text baselines

Not implemented yet. This section is the design for when a dialog needs
it.

### The problem

In a row of controls, the text of each one should be on the same line. A
horizontal stack centres its children by default, and for the usual row
that does it:

| Beside a 14-high edit box, combo box or button | Offset from centring |
|---|---|
| One-line label (8 high) | (14 − 8) / 2 = 3 |
| Check box or radio button (10 high), whose text is 1 down inside it | (14 − 10) / 2 = 2, so its text is at 3 |

3 is the offset that Microsoft's guidelines give for a label beside a text
box. Centring goes wrong in two cases:

- **A label of two or more lines beside a one-line control.** Centring
  puts the middle of the label level with the middle of the control, but
  its first line should be level with the control's text.
- **A one-line label beside a taller control**, such as a list box or a
  multi-line edit box. The label should be level with the first line at the
  top of the control, not halfway down it. (Beside a trackbar, which has no
  text, centring is what is wanted.)

The workaround until this is implemented is to place the label by hand:
`alignSelf="start" margin="3 0 0 0"`.

### The design

As in CSS's `align-items: baseline`, a new alignment value `baseline` for
the `align` of an `hstack` and for `alignSelf`. Each node may have a
**first-line offset** `b(c)`: the distance from the top of its border box
to the top of its first line of text.

| Node | `b(c)` |
|---|---|
| Label | 0 |
| Check box, radio button | 1 |
| Edit box (one line or multi-line), combo box, button | 3 |
| List box | about 1, to be checked |
| Trackbar, spacer, `<control>` | none |
| `hstack` aligned by baseline | its top padding + `above` (below) |
| `vstack` | its top padding + the top margin and `b` of its first child, if that has one |
| Group box, other `hstack` | none |

A control could also take a `baseline` attribute to set or override
`b(c)`, for a `<control>` of a class the table does not know.

In an `hstack` aligned by baseline, the **participants** are the children
whose alignment is `baseline` and that have a first-line offset. They are
placed so that their first lines are level, and the rest are placed by
their own `alignSelf`, or at the start. In the notation of the formulas
above, where *across* is vertical for an `hstack`, and with `top(M)` and
`bottom(M)` the top and bottom margins:

```
    above  = max over participants of (top(M(c)) + b(c))
    below  = max over participants of (height(m(c)) - b(c) + bottom(M(c)))

    measure:
        across = vertical(P) + max(above + below,
                                   max over the others of (height(m(c)) + vertical(M(c))))

    arrange, for a participant:
        top of border box = top of C + above - b(c)
```

If the stack is taller than it needs to be, the participants stay at the
top. `baseline` in a `vstack` is an error, as there is no row to line up.

Checking against the cases above:

- Label (8 high, `b` 0) beside an edit box (14, 3): `above` = 3,
  `below` = max(8, 11) = 11, so the row is 14 high, with the label at 3 and
  the edit box at 0. This is the same as centring.
- A two-line label (16 high) beside it: `below` = max(16, 11) = 16, so the
  row is 19 high, with the label still at 3 and the edit box at 0.
- A check box (10, 1) beside an edit box: the check box is at 3 − 1 = 2, as
  with centring.

### Implementing it

- `Alignment.Baseline`, accepted by the reader for an `hstack`'s `align` and
  for `alignSelf`, with an error for a `vstack` or group.
- A first-line offset on each node, set by `Layout.Measure` from the table,
  and the formulas above in the measure and arrange passes of an `hstack`.
- Tests: the three cases above; a nested `hstack`, whose own offset lets it
  line up with its siblings; a participant with margins; a trackbar beside
  labels, which is not a participant.

Whether `baseline` should then become the default `align` of an `hstack`
can be decided once it has been tried. It gives the same result as centring
for one-line labels and controls, but a row with a trackbar would need
`align="center"` written out.

### Pixels

No layout in dialog units can promise baselines exact to the pixel. Windows
converts each control's rectangle to pixels separately, rounding each
coordinate, and each control then places its own text in pixels. At 150%,
texts that are level in dialog units can be a pixel apart. The offsets in
the table are what the guidelines imply; only rendering the dialog can
check them, and those marked "to be checked" have not been.
