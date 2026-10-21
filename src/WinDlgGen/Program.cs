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
    Arity = ArgumentArity.OneOrMore,
};

Option<FileInfo> outputFileOpt = new("--output", "-o")
{
    Description = "RC file to write to; if not specified, writes to stdout"
};

Option<FileInfo> includeFileOpt = new("--include", "-i")
{
    Description = "Text file containing definitions to be inserted into output"
};

RootCommand rootCommand =
    new("Lays out the dialogs described by the XML files, and writes them as DIALOGEX resources. ")
    {
        outputFileOpt,
        includeFileOpt,
        inputXmlArgs,
    };

rootCommand.SetAction(parseResult =>
{
    var inputFilePaths = parseResult.GetValue(inputXmlArgs)!.Select(f => f.FullName);
    var outputFilePath = parseResult.GetValue(outputFileOpt)?.FullName;
    var includeFilePath = parseResult.GetValue(includeFileOpt)?.FullName;
    return Run(inputFilePaths, outputFilePath, includeFilePath);
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

static int Run(IEnumerable<string> inputFilePaths, string? outputFilePath, string? includeFilePath)
{
    var diagnostics = new DiagnosticBag();
    var writer = new RcWriter(diagnostics);

    if (includeFilePath is not null)
    {
        try
        {
            using var includeFileReader = File.OpenText(includeFilePath);
            writer.CopyVerbatim(includeFileReader, Path.GetFileName(includeFilePath));
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            diagnostics.Error(DiagnosticCodes.FileError, new SourceLocation(includeFilePath, 0, 0), e.Message);
        }
    }

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
    return WriteOutputFile(outputFilePath, bytes);
}

//
// Write output
//

static int WriteOutputFile(string? outputFilePath, ReadOnlySpan<byte> bytes)
{
    // The output is UTF-8 without a byte order mark, as the #pragma code_page
    // at its start declares.
    if (outputFilePath is null)
    {
        using var stdout = Console.OpenStandardOutput();
        stdout.Write(bytes);
        return 0;
    }

    try
    {
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
