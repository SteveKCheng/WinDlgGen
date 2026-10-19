using System.Text;
using System.CommandLine;
using WinDlgGen;

// Command line: WinDlgGen [-o output.rc] input.xml...
//
// Exit codes: 0 on success, 1 if any input had errors (nothing is written
// then), 2 for a bad command line.

Argument<FileInfo[]> inputXmlArgs = new("input")
{
    Description = "XML input file",
    Arity = ArgumentArity.OneOrMore
};

Option<FileInfo> outputFileOpt = new("--output", "-o")
{
    Description = "RC file to write to; if not specified, writes to stdout"
};

RootCommand rootCommand =
    new("Lays out the dialogs described by the XML files, and writes them as DIALOGEX resources. ")
    {
        outputFileOpt,
        inputXmlArgs,
    };

rootCommand.SetAction(parseResult =>
{
    var inputFilePaths = parseResult.GetValue(inputXmlArgs)!.Select(f => f.FullName);
    var outputFilePath = parseResult.GetValue(outputFileOpt)?.FullName;
    return Run(inputFilePaths, outputFilePath);
});

var parseResult = rootCommand.Parse(args);
if (parseResult.Errors.Count > 0)
{
    foreach (var parseError in parseResult.Errors)
        Console.Error.WriteLine(parseError.Message);
    return 2;
}

return parseResult.Invoke();

// 
// Start of processing
//

static int Run(IEnumerable<string> inputFilePaths, string? outputFilePath)
{
    var diagnostics = new DiagnosticBag();
    var writer = new RcWriter(diagnostics);

    foreach (var input in inputFilePaths)
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

    var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(writer.ToString());
    return WriteFileChecked(outputFilePath, bytes);
}

//
// Write output
//

static int WriteFileChecked(string? outputFilePath, ReadOnlySpan<byte> bytes)
{
    // The output is UTF-8 without a byte order mark, as the #pragma code_page
    // at its start declares.
    if (outputFilePath is null)
    {
        using var stdout = Console.OpenStandardOutput();
        stdout.Write(bytes);
        return 0;
    }

    // Leave an unchanged file alone, so that its timestamp does not make the
    // resource compiler run again.
    try
    {
        if (!File.Exists(outputFilePath) || !File.ReadAllBytes(outputFilePath).AsSpan().SequenceEqual(bytes))
            File.WriteAllBytes(outputFilePath, bytes);
    }
    catch (Exception e) when (e is IOException or UnauthorizedAccessException)
    {
        Console.Error.WriteLine(new Diagnostic(Severity.Error, DiagnosticCodes.FileError,
            new SourceLocation(outputFilePath, 0, 0), e.Message));
        return 1;
    }
    
    return 0;
}
