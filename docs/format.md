# The XML format

Each XML file describes one dialog. Its root is a `<dialog>` element,
whose children are laid out from top to bottom. Other elements are either
containers, which arrange their children, or controls, which are the
leaves. An example is `samples/options.xml`.

```xml
<dialog id="IDD_EXAMPLE" caption="Example">
  <group text="Display">
    <hstack>
      <label text="Scale by:" width="30"/>
      <combobox id="IDC_SCALE" editable="true" width="38"/>
      <label text="%" width="8"/>
    </hstack>
    <checkbox id="IDC_FULLSCREEN" text="Full-screen mode"/>
  </group>
  <hstack>
    <spacer grow="1"/>
    <button id="IDOK" text="OK" default="true"/>
    <button id="IDCANCEL" text="Cancel"/>
  </hstack>
</dialog>
```

All sizes are in dialog units: whole numbers from 0 to 32767. A
horizontal dialog unit is a quarter of the dialog font's average character
width, and a vertical one is an eighth of its height, so Windows scales the
dialog with its font and with the DPI. How containers place their children
is described in [layout.md](layout.md).

The reader is strict. An unknown element or attribute, a value of the wrong
form, text between elements, and a DTD are all errors, reported with the
line and column in the format Visual Studio's Error List understands.

## Attributes of every node

These apply to every element except `<dialog>`; `<spacer>` takes only
`width`, `height` and `grow`.

| Attribute | Value | Meaning |
|---|---|---|
| `width`, `height` | dialog units | For a control or spacer, its size, which a container does not stretch. Without it, the control is 0 in that direction until stretched. For a container, a minimum. |
| `margin` | 1–4 numbers | Space around the node, added to its container's spacing. The numbers are as in CSS: all sides; vertical and horizontal; top, horizontal and bottom; or top, right, bottom and left. Default 0. |
| `grow` | number | Weight for sharing out space the container has beyond what its children need, along its direction. Default 0: the node does not grow. |
| `alignSelf` | `start`, `center`, `end`, `stretch` | Placement across the container, overriding its `align`. |

## Containers

### `<dialog>`

The root. It is a vertical stack whose padding is the dialog's margin.

| Attribute | Default | Meaning |
|---|---|---|
| `id` | required | Resource identifier: a C identifier, or a whole number. |
| `caption` | none | Title bar text. |
| `kind` | `modal` | `modal` for a dialog with a frame and title bar; `child` for one placed inside another window, such as a property page. |
| `center` | true for `modal` | Adds `DS_CENTER`. |
| `style` | see below | Replaces the default window style. |
| `exstyle` | none | Extended window style. |
| `fontSize`, `fontFace` | 8, `MS Shell Dlg 2` | The dialog font, which also sets the size of a dialog unit. `MS Shell Dlg 2` is mapped to Tahoma. |
| `padding` | 7 | Margin between the dialog's edges and its contents. |
| `spacing` | 7 | Gap between its children, which are usually unrelated groups. |
| `align` | `stretch` | Placement of children across the dialog. |
| `width`, `height` | none | Minimum size. |

The default style of a modal dialog is
`DS_MODALFRAME | WS_POPUP | WS_CAPTION | WS_SYSMENU`, and of a child dialog
`DS_CONTROL | WS_CHILD`. `DS_SETFONT` is always added, since `FONT` needs
it, and `DS_CENTER` as `center` says. Add `WS_VISIBLE` to `style` for a
modeless dialog that should show itself when created.

### `<vstack>` and `<hstack>`

Place their children one after another, from top to bottom or from left to
right, without drawing anything.

| Attribute | Default | Meaning |
|---|---|---|
| `spacing` | 4 | Gap between adjacent children. |
| `padding` | 0 | Space between the edges and the children; 1–4 numbers as for `margin`. |
| `align` | `stretch` for `vstack`, `center` for `hstack` | Placement of children across the stack. Centring in a horizontal stack lines up the text of a one-line label with that of the edit box or combo box beside it. |

### `<group>`

A group box: a vertical stack with a frame and caption drawn around it.

| Attribute | Default | Meaning |
|---|---|---|
| `text` | empty | The caption. |
| `padding` | `11 7 7 7` | Space inside the frame. The top clears the caption. |
| `spacing` | 4 | Gap between adjacent children. |
| `align` | `stretch` | As for `vstack`. |

It also takes the control attributes `id`, `style`, `exstyle`, `visible`,
`enabled`, `tabstop` and `group` below, which apply to the group box
control.

### `<spacer>`

Empty space that writes nothing. A stack leaves no spacing on either side of
a spacer, so a spacer of a given size is exactly the gap between its
neighbours, and `<spacer grow="1"/>` pushes the following children to the
far end.

## Controls

### Attributes of every control

| Attribute | Default | Meaning |
|---|---|---|
| `id` | required for interactive controls; else `IDC_STATIC` | A C identifier, defined elsewhere in the `.rc` file (usually in `resource.h`), or a whole number. Each may be used only once in a file, except `IDC_STATIC`. |
| `text` | empty | The window text. `&` marks the access key, as in a `.rc` file, so it is written `&amp;` in XML. A line break is written `&#10;`. |
| `style` | none | Further style flags, joined by `\|`, added to those the element implies. |
| `exstyle` | none | Extended style flags. |
| `tabstop` | by kind and position | Sets or clears `WS_TABSTOP`. |
| `group` | by kind and position | Sets or clears `WS_GROUP`. |
| `visible` | true | False to create the control hidden. It still takes up its space. |
| `enabled` | true | False to create the control disabled. |

`style` and `exstyle` may contain only symbols, numbers, `|` and
parentheses. `NOT` is refused, since what it removes depends on where it
stands and on the resource compiler's defaults; use `tabstop`, `group`,
`visible` and `enabled` instead, or a dialog's `style`.

By default every control starts a group (`WS_GROUP`), except a radio button
that follows another one: adjacent radio buttons form one group, within
which the arrow keys move. Interactive controls are tab stops; of a group of
radio buttons, only the first is, and tabbing into the group moves to the
checked one.

### The elements

| Element | Class and style | Default size | Own attributes |
|---|---|---|---|
| `<label>` | `Static`, `SS_LEFT` | height 8 | `textAlign` = `left`, `center` or `right`; `noPrefix` = true for `SS_NOPREFIX`, to show `&` as it is. |
| `<button>` | `Button`, `BS_PUSHBUTTON` | 50 × 14 | `default` = true for `BS_DEFPUSHBUTTON`. |
| `<checkbox>` | `Button`, `BS_AUTOCHECKBOX` | height 10 | `threeState` = true for `BS_AUTO3STATE`. |
| `<radio>` | `Button`, `BS_AUTORADIOBUTTON` | height 10 | |
| `<edit>` | `Edit`, `ES_LEFT \| ES_AUTOHSCROLL \| WS_BORDER` | height 14 | `multiline` (replaces `ES_AUTOHSCROLL` with `ES_MULTILINE \| ES_AUTOVSCROLL \| ES_WANTRETURN \| WS_VSCROLL`), `password`, `number`, `readOnly`. |
| `<combobox>` | `ComboBox`, `CBS_DROPDOWNLIST \| WS_VSCROLL` | height 14 | `editable` = true for `CBS_DROPDOWN \| CBS_AUTOHSCROLL`; `sort`; `listHeight`, default 80. |
| `<listbox>` | `ListBox`, `LBS_NOTIFY \| LBS_NOINTEGRALHEIGHT \| WS_VSCROLL \| WS_BORDER` | none | `sort`. |
| `<trackbar>` | `msctls_trackbar32`, `TBS_HORZ` | height 15 | `ticks` = true for `TBS_AUTOTICKS`. |
| `<control>` | the `class` attribute | none | `class`, required. |

A combo box's `height` is that of its visible part, which is what the
layout uses. Its open list is `listHeight` more, which is added only in
the output, since a dialog template gives a combo box's height with the
list included.

Labels and controls without a `width` are meant to be stretched to the
width of their container, as they are by default in a vertical stack. One
that ends up with no width or height is an error.

## The output

`WinDlgGen -o dialogs.rc a.xml b.xml` writes one `DIALOGEX` resource per
input file, and leaves `dialogs.rc` untouched if it would not change. The
file is UTF-8, and starts with `#pragma code_page(65001)`. 

The option `-i` may be used to paste in whether definitions are needed 
for the output `.rc` file to compile.  Typically the following files need to
be the target of `#include` directives inside the file named by option `-i`:

  - `winres.h` (from the Windows SDK; defines `IDC_STATIC`)
  - `commctrl.h` (if the dialog resource uses common controls)
  - `resource.h` (defined by the program that uses the dialog resource).

Currently, WinDlgGen does not parse the definitions in the file `-i`,
but just inserts their contents.  A future version might read the definitions
to be able to display a preview of the dialog.

Every control is written as a `CONTROL` statement with all its style flags
spelled out. The resource compiler's shorthand statements, such as `LTEXT`
and `PUSHBUTTON`, each add default flags of their own, which would make the
output harder to check against the XML. `CONTROL` itself adds only
`WS_CHILD | WS_VISIBLE`.
