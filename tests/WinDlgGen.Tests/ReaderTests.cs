using System.Text;
using static WinDlgGen.Tests.TestUtil;

namespace WinDlgGen.Tests;

public class ReaderTests
{
    private static DiagnosticBag Read(string xml)
    {
        var diagnostics = new DiagnosticBag();
        DialogReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(xml)), "test.xml", diagnostics);
        return diagnostics;
    }

    [Theory]
    [InlineData("""<dialog id="D"><button id="B" widht="5"/></dialog>""", DiagnosticCodes.UnknownAttribute)]
    [InlineData("""<dialog id="D"><buton id="B"/></dialog>""", DiagnosticCodes.UnknownElement)]
    [InlineData("""<dialog id="D"><button/></dialog>""", DiagnosticCodes.MissingAttribute)]
    [InlineData("""<dialog/>""", DiagnosticCodes.MissingAttribute)]
    [InlineData("""<dialog id="D"><control id="X"/></dialog>""", DiagnosticCodes.MissingAttribute)]
    [InlineData("""<dialog id="D"><button id="B" width="-5"/></dialog>""", DiagnosticCodes.BadValue)]
    [InlineData("""<dialog id="D"><button id="B" default="yes"/></dialog>""", DiagnosticCodes.BadValue)]
    [InlineData("""<dialog id="D"><button id="1B"/></dialog>""", DiagnosticCodes.BadValue)]
    [InlineData("""<dialog id="D"><hstack align="middle"/></dialog>""", DiagnosticCodes.BadValue)]
    [InlineData("""<dialog id="D"><button id="B" style="WS_BORDER | NOT WS_TABSTOP"/></dialog>""", DiagnosticCodes.BadValue)]
    [InlineData("""<dialog id="D"><button id="B" style="WS_BORDER, 1, 2"/></dialog>""", DiagnosticCodes.BadValue)]
    [InlineData("""<dialog id="D"><button id="B" style="WS_BORDER |"/></dialog>""", DiagnosticCodes.BadValue)]
    [InlineData("""<dialog id="D"><button id="B" text="a&#127;b"/></dialog>""", DiagnosticCodes.BadValue)]
    [InlineData("""<dialog id="D">OK<button id="B"/></dialog>""", DiagnosticCodes.BadText)]
    [InlineData("""<dialog id="D"><button id="B">OK</button></dialog>""", DiagnosticCodes.BadText)]
    [InlineData("""<dialog id="D"><button id="B"/><checkbox id="B"/></dialog>""", DiagnosticCodes.DuplicateId)]
    [InlineData("""<dialog id="D"><x:button xmlns:x="urn:x" id="B"/></dialog>""", DiagnosticCodes.UnknownElement)]
    [InlineData("""<dialogs/>""", DiagnosticCodes.UnknownElement)]
    [InlineData("""<dialog id="D">""", DiagnosticCodes.XmlSyntax)]
    public void ReportsProblem(string xml, string code)
        => Assert.Equal([code], Codes(Read(xml)));

    [Fact]
    public void RefusesDtd()
    {
        var diagnostics = Read("""
            <!DOCTYPE dialog [<!ENTITY x "OK">]>
            <dialog id="D"><button id="B" text="&x;"/></dialog>
            """);
        Assert.Equal([DiagnosticCodes.XmlSyntax], Codes(diagnostics));
    }

    [Fact]
    public void ReportsLineAndColumn()
    {
        var d = Assert.Single(Read("""
            <dialog id="D">
              <button id="B" colour="red"/>
            </dialog>
            """).Items);
        Assert.Equal(new SourceLocation("test.xml", 2, 18), d.Location);
        Assert.Equal("test.xml(2,18): error WDG1002: <button> has no attribute colour", d.ToString());
    }

    [Fact]
    public void StaticIdMayRepeat()
        => Assert.Empty(Read("""
            <dialog id="D"><label text="a" id="IDC_STATIC"/><label text="b"/></dialog>
            """).Items);

    [Fact]
    public void ReportsSeveralProblems()
        => Assert.Equal(
            [DiagnosticCodes.BadValue, DiagnosticCodes.UnknownAttribute, DiagnosticCodes.UnknownElement],
            Codes(Read("""<dialog id="D"><button id="B" width="x" foo="1"/><bar/></dialog>""")));

    [Fact]
    public void SampleGeneratesWithoutDiagnostics()
    {
        var dir = AppContext.BaseDirectory;
        while (!File.Exists(Path.Combine(dir, "WinDlgGen.slnx")))
            dir = Path.GetDirectoryName(dir) ?? throw new FileNotFoundException("Cannot find the repository root");

        foreach (var path in Directory.GetFiles(Path.Combine(dir, "samples"), "*.xml"))
        {
            var diagnostics = new DiagnosticBag();
            var dialog = DialogReader.ReadFile(path, path, diagnostics);
            Assert.NotNull(dialog);
            Layout.Run(dialog, diagnostics);
            new RcWriter(diagnostics).Add(dialog, Path.GetFileName(path));
            Assert.Empty(diagnostics.Items);
        }
    }
}
