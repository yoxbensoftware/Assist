namespace Assist;

/// <summary>
/// Text diff tool to compare two text inputs line by line.
/// </summary>
public sealed class TextDiffForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);

    private readonly TextBox _txtLeft = null!;
    private readonly TextBox _txtRight = null!;
    private readonly TextBox _txtDiff = null!;
    private readonly Label _lblStatus = null!;

    public TextDiffForm()
    {
        Text = "Text Diff Tool";
        ClientSize = new Size(1200, 700);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== TEXT DIFF TOOL ===",
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        var lblLeft = new Label
        {
            Text = "Text 1 (Sol):",
            Location = new Point(20, 60),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtLeft = new TextBox
        {
            Location = new Point(20, 85),
            Width = 560,
            Height = 200,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false
        };

        var lblRight = new Label
        {
            Text = "Text 2 (Sağ):",
            Location = new Point(600, 60),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtRight = new TextBox
        {
            Location = new Point(600, 85),
            Width = 580,
            Height = 200,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false
        };

        var btnCompare = new Button
        {
            Text = "Compare",
            Location = new Point(20, 295),
            Width = 150,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnCompare.FlatAppearance.BorderColor = GreenText;
        btnCompare.Click += (_, _) => CompareDiff();

        var btnClear = new Button
        {
            Text = "Clear All",
            Location = new Point(180, 295),
            Width = 120,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnClear.FlatAppearance.BorderColor = GreenText;
        btnClear.Click += (_, _) => 
        { 
            _txtLeft?.Clear(); 
            _txtRight?.Clear(); 
            _txtDiff?.Clear(); 
            if (_lblStatus is not null) 
                _lblStatus.Text = "Status: Ready"; 
        };

        _lblStatus = new Label
        {
            Text = "Status: Ready",
            Location = new Point(20, 335),
            Width = 1160,
            Height = 20,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9)
        };

        var lblDiff = new Label
        {
            Text = "Diff Results (+ Added, - Removed, ~ Changed):",
            Location = new Point(20, 365),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtDiff = new TextBox
        {
            Location = new Point(20, 390),
            Width = 1160,
            Height = 290,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false
        };

        Controls.AddRange([lblTitle, lblLeft, _txtLeft, lblRight, _txtRight, btnCompare, btnClear, _lblStatus, lblDiff, _txtDiff]);
    }

    private void CompareDiff()
    {
        try
        {
            var lines1 = _txtLeft.Text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);
            var lines2 = _txtRight.Text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

            if (lines1.SequenceEqual(lines2))
            {
                _lblStatus.Text = "Status: ✓ Metinler tamamen aynı (Identical)";
                _lblStatus.ForeColor = GreenText;
                _txtDiff.Text = "No differences found. Texts are identical.";
                return;
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine("╔════════════════════════════════════════════════════════════════════════╗");
            result.AppendLine("║                           DIFF RESULTS                                  ║");
            result.AppendLine("╚════════════════════════════════════════════════════════════════════════╝");
            result.AppendLine();

            int maxLines = Math.Max(lines1.Length, lines2.Length);
            int differences = 0;

            for (int i = 0; i < maxLines; i++)
            {
                var line1 = i < lines1.Length ? lines1[i] : null;
                var line2 = i < lines2.Length ? lines2[i] : null;

                if (line1 == null && line2 != null)
                {
                    result.AppendLine($"[Line {i + 1}] + ADDED: {line2}");
                    differences++;
                }
                else if (line1 != null && line2 == null)
                {
                    result.AppendLine($"[Line {i + 1}] - REMOVED: {line1}");
                    differences++;
                }
                else if (line1 != line2)
                {
                    result.AppendLine($"[Line {i + 1}] ~ CHANGED:");
                    result.AppendLine($"  < Text1: {line1}");
                    result.AppendLine($"  > Text2: {line2}");
                    differences++;
                }
            }

            result.AppendLine();
            result.AppendLine(new string('─', 75));
            result.AppendLine($"Total Lines (Text1): {lines1.Length}");
            result.AppendLine($"Total Lines (Text2): {lines2.Length}");
            result.AppendLine($"Differences Found: {differences}");

            _txtDiff.Text = result.ToString();
            _lblStatus.Text = $"Status: ✓ {differences} fark bulundu";
            _lblStatus.ForeColor = GreenText;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Status: ✗ Karşılaştırma Hatası - {ex.Message}";
            _lblStatus.ForeColor = Color.Red;
        }
    }
}
