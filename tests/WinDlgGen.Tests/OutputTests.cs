using static WinDlgGen.Tests.TestUtil;

namespace WinDlgGen.Tests;

public class OutputTests
{
    [Fact]
    public void WritesDialogHeader()
    {
        var rc = Generate("""<dialog id="IDD_X" caption="Title"><button id="IDOK" text="OK" default="true"/></dialog>""");
        Assert.Contains("""
            IDD_X DIALOGEX 0, 0, 64, 28
            STYLE DS_SETFONT | DS_CENTER | DS_MODALFRAME | WS_POPUP | WS_CAPTION | WS_SYSMENU
            CAPTION "Title"
            FONT 8, "MS Shell Dlg 2", 0, 0, 0x1
            BEGIN
                CONTROL "OK", IDOK, "Button", BS_DEFPUSHBUTTON | WS_GROUP | WS_TABSTOP, 7, 7, 50, 14
            END
            """.ReplaceLineEndings("\r\n"), rc);
    }

    [Fact]
    public void ChildDialogHasNoFrame()
    {
        var rc = Generate("""<dialog id="D" kind="child"><button id="B"/></dialog>""");
        Assert.Contains("\r\nSTYLE DS_SETFONT | DS_CONTROL | WS_CHILD\r\n", rc);
    }

    [Fact]
    public void DialogStyleReplacesFrame()
    {
        var rc = Generate("""<dialog id="D" center="false" style="WS_POPUP | WS_BORDER"><button id="B"/></dialog>""");
        Assert.Contains("\r\nSTYLE DS_SETFONT | WS_POPUP | WS_BORDER\r\n", rc);
    }

    [Fact]
    public void AdjacentRadioButtonsFormOneGroup()
    {
        var rc = Generate("""
            <dialog id="D" width="100">
              <radio id="R1" text="1"/>
              <radio id="R2" text="2"/>
              <checkbox id="C" text="c"/>
              <radio id="R3" text="3"/>
            </dialog>
            """);
        Assert.Contains("BS_AUTORADIOBUTTON | WS_GROUP | WS_TABSTOP,", ControlLine(rc, "R1"));
        Assert.Contains("\"Button\", BS_AUTORADIOBUTTON, ", ControlLine(rc, "R2"));
        Assert.Contains("BS_AUTOCHECKBOX | WS_GROUP | WS_TABSTOP,", ControlLine(rc, "C"));
        Assert.Contains("BS_AUTORADIOBUTTON | WS_GROUP | WS_TABSTOP,", ControlLine(rc, "R3"));
    }

    [Fact]
    public void GroupBoxComesBeforeItsContents()
    {
        var rc = Generate("""
            <dialog id="D" width="100">
              <group text="G"><checkbox id="C" text="c"/></group>
            </dialog>
            """);
        int box = rc.IndexOf("\"Button\", BS_GROUPBOX | WS_GROUP, 7, 7, 86, 28");
        Assert.True(box >= 0);
        Assert.True(box < rc.IndexOf(", C, "));
    }

    [Fact]
    public void ExplicitFlagsOverrideDefaults()
    {
        var rc = Generate("""
            <dialog id="D" width="100">
              <label id="L" text="x" group="false"/>
              <control id="X" class="SysLink" tabstop="true" visible="false" enabled="false" style="LWS_TRANSPARENT" exstyle="WS_EX_TRANSPARENT" height="8"/>
            </dialog>
            """);
        Assert.Contains("\"Static\", SS_LEFT, 7, 7, 86, 8", ControlLine(rc, "L"));
        Assert.EndsWith(
            "\"SysLink\", LWS_TRANSPARENT | WS_GROUP | WS_TABSTOP | WS_DISABLED | NOT WS_VISIBLE, 7, 22, 86, 8, WS_EX_TRANSPARENT",
            ControlLine(rc, "X"));
    }

    [Fact]
    public void ComboBoxHeightIncludesList()
    {
        var rc = Generate("""
            <dialog id="D" width="100">
              <combobox id="A"/>
              <combobox id="B" editable="true" sort="true" listHeight="40"/>
            </dialog>
            """);
        Assert.EndsWith("\"ComboBox\", CBS_DROPDOWNLIST | WS_VSCROLL | WS_GROUP | WS_TABSTOP, 7, 7, 86, 94", ControlLine(rc, "A"));
        // The list does not take up space: B follows the visible part of A.
        Assert.EndsWith(
            "\"ComboBox\", CBS_DROPDOWN | CBS_AUTOHSCROLL | CBS_SORT | WS_VSCROLL | WS_GROUP | WS_TABSTOP, 7, 28, 86, 54",
            ControlLine(rc, "B"));
    }

    [Fact]
    public void LabelsDefaultToStaticId()
    {
        var rc = Generate("""<dialog id="D"><label text="x" width="10" textAlign="right" noPrefix="true"/></dialog>""");
        Assert.Contains("CONTROL \"x\", IDC_STATIC, \"Static\", SS_RIGHT | SS_NOPREFIX | WS_GROUP, ", rc);
    }

    [Theory]
    [InlineData("plain", "\"plain\"")]
    [InlineData("say \"hi\"", "\"say \"\"hi\"\"\"")]
    [InlineData(@"C:\dir", @"""C:\\dir""")]
    [InlineData("a\nb\tc", @"""a\nb\tc""")]
    [InlineData("&Options…", "\"&Options…\"")]
    public void QuotesTextForRc(string text, string expected)
        => Assert.Equal(expected, RcWriter.Quote(text));

    [Fact]
    public void NewlineInAttributeReachesOutput()
    {
        var rc = Generate("""<dialog id="D"><label text="one&#10;two" width="20" height="16"/></dialog>""");
        Assert.Contains("CONTROL \"one\\ntwo\", ", rc);
    }

    [Fact]
    public void OutputStartsWithCodePage()
    {
        var rc = Generate("""<dialog id="D"><button id="B"/></dialog>""");
        Assert.Contains("\r\n#pragma code_page(65001)\r\n", rc);
        Assert.DoesNotContain("\n", rc.Replace("\r\n", ""));
    }
}
