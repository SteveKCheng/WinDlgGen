using static WinDlgGen.Tests.TestUtil;

namespace WinDlgGen.Tests;

public class LayoutTests
{
    [Fact]
    public void DialogMarginSurroundsContents()
    {
        var d = LayOut("""<dialog id="D"><button id="B" text="OK"/></dialog>""");
        Assert.Equal(new Rect(7, 7, 50, 14), BoundsOf(d, "B"));
        Assert.Equal(new Rect(0, 0, 64, 28), d.Bounds);
    }

    [Fact]
    public void VerticalStackLeavesSpacingAndStretches()
    {
        var d = LayOut("""
            <dialog id="D" width="100">
              <vstack>
                <checkbox id="A" text="a"/>
                <checkbox id="B" text="b"/>
              </vstack>
            </dialog>
            """);
        Assert.Equal(new Rect(7, 7, 86, 10), BoundsOf(d, "A"));
        Assert.Equal(new Rect(7, 21, 86, 10), BoundsOf(d, "B"));
    }

    [Fact]
    public void DialogChildrenAreSpacedAsUnrelated()
    {
        var d = LayOut("""<dialog id="D"><button id="A"/><button id="B"/></dialog>""");
        Assert.Equal(7 + 14 + 7, BoundsOf(d, "B").Y);
    }

    [Fact]
    public void SpacerReplacesSpacing()
    {
        var d = LayOut("""
            <dialog id="D">
              <hstack><button id="A"/><spacer width="10"/><button id="B"/></hstack>
            </dialog>
            """);
        Assert.Equal(7 + 50 + 10, BoundsOf(d, "B").X);
    }

    [Fact]
    public void GrowSharesExtraSpaceByWeight()
    {
        // Content width 100; the button takes 50, leaving 50 shared 1:2.
        // The first spacer rounds down to 16, and the last takes the rest.
        var d = LayOut("""
            <dialog id="D" width="114">
              <hstack>
                <spacer grow="1"/><button id="B"/><spacer grow="2"/>
              </hstack>
            </dialog>
            """);
        Assert.Equal(7 + 16, BoundsOf(d, "B").X);
    }

    [Fact]
    public void GrowEnlargesControl()
    {
        var d = LayOut("""
            <dialog id="D" width="134">
              <hstack><button id="A"/><button id="B" grow="1"/></hstack>
            </dialog>
            """);
        Assert.Equal(new Rect(7 + 50 + 4, 7, 50 + 16, 14), BoundsOf(d, "B"));
    }

    [Fact]
    public void HorizontalStackCentresLabelBesideEdit()
    {
        var d = LayOut("""
            <dialog id="D">
              <hstack><label id="L" text="Name:" width="30"/><edit id="E" width="60"/></hstack>
            </dialog>
            """);
        Assert.Equal(new Rect(7, 10, 30, 8), BoundsOf(d, "L"));
        Assert.Equal(new Rect(41, 7, 60, 14), BoundsOf(d, "E"));
    }

    [Fact]
    public void StretchKeepsExplicitWidth()
    {
        var d = LayOut("""
            <dialog id="D" width="200">
              <button id="B"/>
              <checkbox id="C" text="c"/>
            </dialog>
            """);
        Assert.Equal(new Rect(7, 7, 50, 14), BoundsOf(d, "B"));
        Assert.Equal(186, BoundsOf(d, "C").Width);
    }

    [Theory]
    [InlineData("start", 7)]
    [InlineData("center", 7 + 68)]
    [InlineData("end", 7 + 136)]
    public void AlignSelfPlacesAcross(string align, int x)
    {
        var d = LayOut($"""
            <dialog id="D" width="200"><button id="B" alignSelf="{align}"/></dialog>
            """);
        Assert.Equal(x, BoundsOf(d, "B").X);
    }

    [Fact]
    public void GroupPaddingClearsCaption()
    {
        var d = LayOut("""
            <dialog id="D" width="100">
              <group id="G" text="Group"><checkbox id="C" text="c"/></group>
            </dialog>
            """);
        Assert.Equal(new Rect(7, 7, 86, 11 + 10 + 7), BoundsOf(d, "G"));
        Assert.Equal(new Rect(14, 18, 72, 10), BoundsOf(d, "C"));
    }

    [Fact]
    public void ContainerWidthIsMinimum()
    {
        var d = LayOut("""
            <dialog id="D">
              <vstack width="20"><button id="B"/></vstack>
            </dialog>
            """);
        Assert.Equal(50, BoundsOf(d, "B").Width);
        Assert.Equal(64, d.Bounds.Width);
    }

    [Fact]
    public void MarginAddsToSpacing()
    {
        var d = LayOut("""
            <dialog id="D"><button id="A"/><button id="B" margin="3 0 0 5"/></dialog>
            """);
        Assert.Equal(new Rect(7 + 5, 7 + 14 + 7 + 3, 50, 14), BoundsOf(d, "B"));
        Assert.Equal(7 + 5 + 50 + 7, d.Bounds.Width);
    }

    [Fact]
    public void UnstretchedControlWithoutWidthIsAnError()
    {
        LayOut("""
            <dialog id="D">
              <hstack><checkbox id="C" text="c"/></hstack>
            </dialog>
            """, out var diagnostics);
        var d = Assert.Single(diagnostics.Items);
        Assert.Equal(DiagnosticCodes.ZeroSize, d.Code);
        Assert.Equal(2, d.Location.Line);
    }

    [Fact]
    public void OversizedDialogIsAnError()
    {
        LayOut("""<dialog id="D" padding="10"><vstack width="32767"/></dialog>""", out var diagnostics);
        Assert.Contains(DiagnosticCodes.TooLarge, Codes(diagnostics));
    }

    [Theory]
    [InlineData("4", 4, 4, 4, 4)]
    [InlineData("1 2", 1, 2, 1, 2)]
    [InlineData("1 2 3", 1, 2, 3, 2)]
    [InlineData("1 2 3 4", 1, 2, 3, 4)]
    public void ThicknessParsesLikeCss(string s, int top, int right, int bottom, int left)
    {
        Assert.True(Thickness.TryParse(s, out var t));
        Assert.Equal(new Thickness(top, right, bottom, left), t);
    }

    [Theory]
    [InlineData("")]
    [InlineData("1 2 3 4 5")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("40000")]
    public void ThicknessRejectsBadValues(string s)
        => Assert.False(Thickness.TryParse(s, out _));
}
