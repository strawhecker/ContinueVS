using System;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

class Program
{
    [STAThread]
    static int Main()
    {
        int fails = 0;
        fails += Probe("Paragraph FontFamily = null", () =>
        {
            var p = new Paragraph();
            p.FontFamily = null;
            p.Inlines.Add(new Run("a"));
            return p.ToString().Length;
        });

        fails += Probe("Paragraph FontFamily = MonospaceFont", () =>
        {
            var p = new Paragraph();
            p.FontFamily = new FontFamily("Consolas, Courier New, Lucida Console");
            p.Inlines.Add(new Run("a"));
            return p.ToString().Length;
        });

        fails += Probe("Run FontFamily = null", () =>
        {
            var r = new Run("a");
            r.FontFamily = null;
            return r.Text.Length;
        });

        fails += Probe("Run FontFamily = FontFamily(\"\")", () =>
        {
            var r = new Run("a");
            r.FontFamily = new FontFamily("");
            return r.Text.Length;
        });

        return fails;
    }

    static int Probe(string label, Func<int> action)
    {
        try
        {
            var n = action();
            Console.WriteLine($"[OK ] {label}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERR] {label} -> {ex.GetType().Name}: {ex.Message}");
            return 1;
        }
    }
}
