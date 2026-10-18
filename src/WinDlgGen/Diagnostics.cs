namespace WinDlgGen;

/// <summary>
/// A position in an input file, for reporting diagnostics.
/// </summary>
/// <param name="File">The file name as it should be reported.</param>
/// <param name="Line">1-based line number, or 0 if unknown.</param>
/// <param name="Column">1-based column number, or 0 if unknown.</param>
public readonly record struct SourceLocation(string File, int Line, int Column)
{
    /// <summary>
    /// Formats the location as MSBuild and Visual Studio expect at the
    /// start of a diagnostic line: <c>file(line,column)</c>, or just the
    /// file name if the line is unknown.
    /// </summary>
    public override string ToString()
        => Line > 0 ? $"{File}({Line},{Column})" : File;
}

/// <summary>Whether a diagnostic stops the output from being written.</summary>
public enum Severity
{
    Warning,
    Error,
}

/// <summary>
/// One problem found in an input file.
/// </summary>
/// <param name="Severity">Errors prevent any output from being written.</param>
/// <param name="Code">Stable identifier such as <c>WDG1001</c>; see
/// <see cref="DiagnosticCodes"/>.</param>
/// <param name="Location">Where the problem is.</param>
/// <param name="Message">Description for the user, as one sentence
/// without a final full stop.</param>
public sealed record Diagnostic(Severity Severity, string Code, SourceLocation Location, string Message)
{
    /// <summary>
    /// Formats the diagnostic in MSBuild's canonical error format, so that
    /// Visual Studio lists it in the Error List and can jump to it.
    /// </summary>
    public override string ToString()
        => $"{Location}: {(Severity == Severity.Error ? "error" : "warning")} {Code}: {Message}";
}

/// <summary>
/// The diagnostic codes, so that each kind of problem keeps its code.
/// </summary>
/// <remarks>
/// 0xxx are problems reading or writing files, 1xxx are problems in the
/// XML, and 2xxx are problems with the layout.
/// </remarks>
public static class DiagnosticCodes
{
    public const string FileError = "WDG0001";

    public const string XmlSyntax = "WDG1000";
    public const string UnknownElement = "WDG1001";
    public const string UnknownAttribute = "WDG1002";
    public const string MissingAttribute = "WDG1003";
    public const string BadValue = "WDG1004";
    public const string BadText = "WDG1005";
    public const string DuplicateId = "WDG1006";

    public const string ZeroSize = "WDG2001";
    public const string TooLarge = "WDG2002";
}

/// <summary>
/// Collects the diagnostics of one run.
/// </summary>
public sealed class DiagnosticBag
{
    private readonly List<Diagnostic> _items = [];

    /// <summary>The diagnostics in the order they were reported.</summary>
    public IReadOnlyList<Diagnostic> Items => _items;

    /// <summary>Whether any diagnostic is an error.</summary>
    public bool HasErrors { get; private set; }

    public void Error(string code, SourceLocation location, string message)
    {
        _items.Add(new Diagnostic(Severity.Error, code, location, message));
        HasErrors = true;
    }

    public void Warning(string code, SourceLocation location, string message)
        => _items.Add(new Diagnostic(Severity.Warning, code, location, message));
}
