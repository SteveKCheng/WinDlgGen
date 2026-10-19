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
WinDlgGen -o dialogs.rc options.xml login.xml
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

The tool is then `src\WinDlgGen\bin\Release\net10.0-windows\WinDlgGen.exe`.

## License

MIT; see [LICENSE.txt](LICENSE.txt).
