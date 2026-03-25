namespace Assist.Forms.DeveloperTools;

using System.Text.RegularExpressions;

/// <summary>
/// Regular expression tester with pattern matching and capture groups display.
/// </summary>
internal sealed class RegexTesterForm : Form
{

    private readonly TextBox _txtPattern = null!;
    private readonly TextBox _txtInput = null!;
    private readonly TextBox _txtMatches = null!;
    private readonly CheckBox _chkIgnoreCase = null!;
    private readonly CheckBox _chkMultiline = null!;
    private readonly Label _lblStatus = null!;

    public RegexTesterForm()
    {
        Text = "Regex Tester";
        ClientSize = new Size(900, 650);
        MinimumSize = new Size(700, 500);
        StartPosition = FormStartPosition.CenterParent;
        AutoScroll = true;
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== REGEX TESTER ===",
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        var lblPattern = new Label
        {
            Text = "Regex Pattern:",
            Location = new Point(20, 60),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtPattern = new TextBox
        {
            Location = new Point(20, 85),
            Width = 860,
            Height = 25,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _chkIgnoreCase = new CheckBox
        {
            Text = "Ignore Case (i)",
            Location = new Point(20, 120),
            Width = 200,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 9)
        };

        _chkMultiline = new CheckBox
        {
            Text = "Multiline (m)",
            Location = new Point(230, 120),
            Width = 200,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 9)
        };

        var lblInput = new Label
        {
            Text = "Test String:",
            Location = new Point(20, 155),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtInput = new TextBox
        {
            Location = new Point(20, 180),
            Width = 860,
            Height = 150,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var btnTest = new Button
        {
            Text = "Test Regex",
            Location = new Point(20, 340),
            Width = 150,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnTest.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnTest.Click += (_, _) => TestRegex();

        var btnClear = new Button
        {
            Text = "Clear All",
            Location = new Point(180, 340),
            Width = 120,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnClear.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnClear.Click += (_, _) =>
        {
            _txtPattern?.Clear();
            _txtInput?.Clear();
            _txtMatches?.Clear();
            if (_lblStatus is not null)
                _lblStatus.Text = "Status: Ready";
        };

        _lblStatus = new Label
        {
            Text = "Status: Ready",
            Location = new Point(20, 380),
            Width = 860,
            Height = 20,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var lblMatches = new Label
        {
            Text = "Matches & Capture Groups:",
            Location = new Point(20, 410),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtMatches = new TextBox
        {
            Location = new Point(20, 435),
            Width = 860,
            Height = 195,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        Controls.AddRange([lblTitle, lblPattern, _txtPattern, _chkIgnoreCase, _chkMultiline, lblInput, _txtInput, btnTest, btnClear, _lblStatus, lblMatches, _txtMatches]);
    }

    private void TestRegex()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_txtPattern.Text))
            {
                _lblStatus.Text = "Status: Hata - Boş pattern";
                _lblStatus.ForeColor = Color.Yellow;
                return;
            }

            if (string.IsNullOrWhiteSpace(_txtInput.Text))
            {
                _lblStatus.Text = "Status: Hata - Boş test string";
                _lblStatus.ForeColor = Color.Yellow;
                return;
            }

            var options = RegexOptions.None;
            if (_chkIgnoreCase.Checked) options |= RegexOptions.IgnoreCase;
            if (_chkMultiline.Checked) options |= RegexOptions.Multiline;

            var regex = new Regex(_txtPattern.Text, options);
            var matches = regex.Matches(_txtInput.Text);

            if (matches.Count == 0)
            {
                _lblStatus.Text = "Status: ✗ Eşleşme yok (No matches)";
                _lblStatus.ForeColor = Color.Yellow;
                _txtMatches.Text = "No matches found.";
                return;
            }

            var result = new System.Text.StringBuilder();
            result.AppendLine($"✓ {matches.Count} eşleşme bulundu:");
            result.AppendLine(new string('─', 80));

            for (int i = 0; i < matches.Count; i++)
            {
                var match = matches[i];
                result.AppendLine($"\n[Match #{i + 1}]");
                result.AppendLine($"  Value: \"{match.Value}\"");
                result.AppendLine($"  Index: {match.Index}");
                result.AppendLine($"  Length: {match.Length}");

                if (match.Groups.Count > 1)
                {
                    result.AppendLine($"  Capture Groups:");
                    for (int g = 1; g < match.Groups.Count; g++)
                    {
                        var group = match.Groups[g];
                        result.AppendLine($"    Group {g}: \"{group.Value}\" (Index: {group.Index})");
                    }
                }
            }

            _txtMatches.Text = result.ToString();
            _lblStatus.Text = $"Status: ✓ {matches.Count} eşleşme bulundu";
            _lblStatus.ForeColor = AppConstants.AccentText;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Status: ✗ Regex Hatası - {ex.Message}";
            _lblStatus.ForeColor = Color.Red;
            _txtMatches.Text = $"Regex Error:\n{ex.Message}";
        }
    }
}
