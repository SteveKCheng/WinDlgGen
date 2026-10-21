# WinDlgGen

A plain-old Windows dialog box generator.

Win32 dialog resources (`DIALOGEX` in a `.rc` file) place every control at
coordinates in dialog units, which are tedious to work out by hand and to
change. WinDlgGen takes a dialog described in XML as nested stacks and group
boxes of controls, lays it out once at development time, and writes the
`DIALOGEX` resource. 

The dialog is then an ordinary Win32 resource: nothing of WinDlgGen is 
needed at run-time.

## Rationale

GUI toolkits like GTK, Qt, WPF and Avalonia let the developer create dialog boxes 
that are resizable by the user at run-time.  They are of course quite heavy
pieces of software.  For applications with simple GUIs, resizable dialog boxes 
may be over-kill.

If those GUI toolkits can be compared to server software for generating the pages
of a Web site dynamically, then WinDlgGen is a *static site generator*.

## Usage

```
WinDlgGen [options] ‹input›…

Arguments:
  ‹input›                 XML input file(s)

Options:
  -i, --include ‹include› Text file holding defeinitions to be inserted into output
  -o, --output ‹output›   RC file to write to; if not specified, writes to stdout
  --help                  Show help and usage information
  --version               Show version information
```

Example:

```
WinDlgGen -i preamble.h -o dialogs.rc options.xml login.xml
```

## Documentation

- [docs/format.md](docs/format.md) describes the XML.
- [docs/layout.md](docs/layout.md) describes how it is laid out.
- [samples/options.xml](samples/options.xml) is an example: the Connection
  Options dialog of [SquirrelVNC](https://github.com/SteveKCheng/SquirrelVNC/).

## Building

With the .NET 10 SDK on Windows:

```
dotnet build -c Release
dotnet test -c Release
```

The tool is then `out\bin\WinDlgGen\Release\WinDlgGen.exe`.

Currently, the tool just reads XML files and writes Win32 resource files, and has
dependencies on the Windows GUI subsystem itself.  It does run on Linux also.

## License

MIT; see [LICENSE.txt](LICENSE.txt).
