# WinDlgGen

A plain old Windows dialog box generator.

Win32 dialog resources (`DIALOGEX` in a `.rc` file) place every control at
coordinates in dialog units, which are tedious to work out by hand and to
change. WinDlgGen takes a dialog described in XML as nested stacks and group
boxes of controls, lays it out once at development time, and writes the
`DIALOGEX` resource. The dialog is then an ordinary resource: nothing of
WinDlgGen is needed at run time.

```
WinDlgGen -o dialogs.rc options.xml login.xml
```

- [docs/format.md](docs/format.md) describes the XML.
- [docs/layout.md](docs/layout.md) describes how it is laid out.
- [samples/options.xml](samples/options.xml) is an example: the Connection
  Options dialog of SquirrelVNC.

## Building

With the .NET 10 SDK on Windows:

```
dotnet build -c Release
dotnet test -c Release
```

The tool is then `src\WinDlgGen\bin\Release\net10.0-windows\WinDlgGen.exe`.

## Licence

MIT; see [LICENSE.txt](LICENSE.txt).
