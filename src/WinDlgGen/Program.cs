using System.Text;
using WinDlgGen;

// Command line: WinDlgGen [-o output.rc] input.xml...
//
// Exit codes: 0 on success, 1 if any input had errors (nothing is written
// then), 2 for a bad command line.

string? output = null;
List<string> inputs = [];

for (int i = 0; i < args.Length; i++)
{
    switch (args[i])
    {
        case "-o" or "--output" when i + 1 < args.Length:
            output = args[++i];
            break;
        case "-o" or "--output":
            Console.Error.WriteLine($"WinDlgGen: {args[i]} needs a file name");
            return 2;
        case "-h" or "--help" or "/?":
            Usage(Console.Out);
            return 0;
        case var arg when arg.StartsWith('-'):
            Console.Error.WriteLine($"WinDlgGen: unknown option {arg}");
            Usage(Console.Error);
            return 2;
        case var arg:
            inputs.Add(arg);
            break;
    }
}

if (inputs.Count == 0)
{
    Usage(Console.Error);
    return 2;
}

var diagnostics = new DiagnosticBag();
var writer = new RcWriter(diagnostics);

foreach (var input in inputs)
{
    DialogNode? dialog;
    try
    {
        dialog = DialogReader.ReadFile(input, input, diagnostics);
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
        diagnostics.Error(DiagnosticCodes.FileError, new SourceLocation(input, 0, 0), e.Message);
        continue;
    }

    if (dialog is null)
        continue;

    // Lay out even after reader errors, to report layout problems too.
    Layout.Run(dialog, diagnostics);
    if (!diagnostics.HasErrors)
        writer.Add(dialog, Path.GetFileName(input));
}

foreach (var d in diagnostics.Items)
    Console.Error.WriteLine(d);

if (diagnostics.HasErrors)
    return 1;

// The output is UTF-8 without a byte order mark, as the #pragma code_page
// at its start declares.
var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(writer.ToString());
if (output is null)
{
    using var stdout = Console.OpenStandardOutput();
    stdout.Write(bytes);
    return 0;
}

// Leave an unchanged file alone, so that its timestamp does not make the
// resource compiler run again.
try
{
    if (!File.Exists(output) || !File.ReadAllBytes(output).AsSpan().SequenceEqual(bytes))
        File.WriteAllBytes(output, bytes);
}
catch (Exception e) when (e is IOException or UnauthorizedAccessException)
{
    Console.Error.WriteLine(new Diagnostic(Severity.Error, DiagnosticCodes.FileError,
                                           new SourceLocation(output, 0, 0), e.Message));
    return 1;
}
return 0;

static void Usage(TextWriter w)
{
    w.WriteLine("Usage: WinDlgGen [-o output.rc] input.xml...");
    w.WriteLine();
    w.WriteLine("Lays out the dialogs described by the XML files, and writes them");
    w.WriteLine("as DIALOGEX resources to output.rc, or to standard output.");
}
