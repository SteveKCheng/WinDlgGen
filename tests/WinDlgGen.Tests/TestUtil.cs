using System.Text;

namespace WinDlgGen.Tests;

/// <summary>Helpers for running the generator on XML in a string.</summary>
internal static class TestUtil
{
    /// <summary>Reads and lays out a dialog, asserting that it parses at all.</summary>
    public static DialogNode LayOut(string xml, out DiagnosticBag diagnostics)
    {
        diagnostics = new DiagnosticBag();
        var dialog = DialogReader.Read(new MemoryStream(Encoding.UTF8.GetBytes(xml)), "test.xml", diagnostics);
        Assert.NotNull(dialog);
        Layout.Run(dialog, diagnostics);
        return dialog;
    }

    /// <summary>Reads and lays out a dialog, asserting that there are no diagnostics.</summary>
    public static DialogNode LayOut(string xml)
    {
        var dialog = LayOut(xml, out var diagnostics);
        Assert.Empty(diagnostics.Items);
        return dialog;
    }

    /// <summary>Generates the .rc text for a dialog, asserting that there are no diagnostics.</summary>
    public static string Generate(string xml)
    {
        var dialog = LayOut(xml, out var diagnostics);
        var writer = new RcWriter(diagnostics);
        writer.Add(dialog, "test.xml");
        Assert.Empty(diagnostics.Items);
        return writer.ToString();
    }

    /// <summary>The CONTROL line written for the control with the given identifier.</summary>
    public static string ControlLine(string rc, string id)
        => Assert.Single(rc.Split("\r\n"), l => l.TrimStart().StartsWith("CONTROL") && l.Contains($", {id}, "));

    /// <summary>The bounds of the control with the given identifier.</summary>
    public static Rect BoundsOf(Node node, string id)
        => Find(node, id)?.Bounds ?? throw new ArgumentException($"No control {id}");

    private static Node? Find(Node node, string id) => node switch
    {
        ControlNode c when c.Spec.Id == id => c,
        GroupNode g when g.Box.Id == id => g,
        StackNode s => s.Children.Select(child => Find(child, id)).FirstOrDefault(n => n is not null),
        _ => null,
    };

    /// <summary>The codes of the diagnostics, in order.</summary>
    public static string[] Codes(DiagnosticBag d) => [.. d.Items.Select(i => i.Code)];
}
