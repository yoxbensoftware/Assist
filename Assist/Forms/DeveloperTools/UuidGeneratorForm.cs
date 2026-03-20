namespace Assist;

/// <summary>
/// UUID/GUID generator with multiple versions and formats.
/// </summary>
public sealed class UuidGeneratorForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);

    private readonly TextBox _txtUuid = null!;
    private readonly TextBox _txtBulk = null!;
    private readonly NumericUpDown _numCount = null!;
    private readonly ComboBox _cmbFormat = null!;
    private readonly Label _lblStatus = null!;

    public UuidGeneratorForm()
    {
        Text = "UUID/GUID Generator";
        ClientSize = new Size(700, 550);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== UUID/GUID GENERATOR ===",
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        var lblFormat = new Label
        {
            Text = "Format:",
            Location = new Point(20, 60),
            Width = 100,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _cmbFormat = new ComboBox
        {
            Location = new Point(130, 57),
            Width = 250,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10),
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat
        };
        _cmbFormat.Items.AddRange(["Standard (with dashes)", "No Dashes", "Uppercase", "Braces {}", "Parentheses ()"]);
        _cmbFormat.SelectedIndex = 0;

        var btnGenerate = new Button
        {
            Text = "Generate UUID",
            Location = new Point(20, 100),
            Width = 180,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnGenerate.FlatAppearance.BorderColor = GreenText;
        btnGenerate.Click += (_, _) => GenerateSingleUuid();

        _txtUuid = new TextBox
        {
            Location = new Point(20, 140),
            Width = 660,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 11, FontStyle.Bold),
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true,
            TextAlign = HorizontalAlignment.Center
        };

        var btnCopy = new Button
        {
            Text = "Copy",
            Location = new Point(210, 100),
            Width = 100,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnCopy.FlatAppearance.BorderColor = GreenText;
        btnCopy.Click += (_, _) => CopyToClipboard(_txtUuid.Text);

        var lblBulk = new Label
        {
            Text = "Bulk Generation:",
            Location = new Point(20, 190),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        var lblCount = new Label
        {
            Text = "Count:",
            Location = new Point(20, 220),
            Width = 80,
            ForeColor = GreenText
        };

        _numCount = new NumericUpDown
        {
            Location = new Point(110, 217),
            Width = 100,
            Minimum = 1,
            Maximum = 1000,
            Value = 10,
            BackColor = Color.Black,
            ForeColor = GreenText,
            BorderStyle = BorderStyle.FixedSingle
        };

        var btnBulkGenerate = new Button
        {
            Text = "Generate Multiple",
            Location = new Point(220, 215),
            Width = 180,
            Height = 25,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnBulkGenerate.FlatAppearance.BorderColor = GreenText;
        btnBulkGenerate.Click += (_, _) => GenerateBulkUuids();

        var btnCopyBulk = new Button
        {
            Text = "Copy All",
            Location = new Point(410, 215),
            Width = 120,
            Height = 25,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnCopyBulk.FlatAppearance.BorderColor = GreenText;
        btnCopyBulk.Click += (_, _) => CopyToClipboard(_txtBulk?.Text ?? string.Empty);

        _txtBulk = new TextBox
        {
            Location = new Point(20, 255),
            Width = 660,
            Height = 250,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false
        };

        _lblStatus = new Label
        {
            Text = "Status: Ready",
            Location = new Point(20, 515),
            Width = 660,
            Height = 20,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9)
        };

        Controls.AddRange([lblTitle, lblFormat, _cmbFormat, btnGenerate, _txtUuid, btnCopy, lblBulk, lblCount, _numCount, btnBulkGenerate, btnCopyBulk, _txtBulk, _lblStatus]);

        // Generate initial UUID
        GenerateSingleUuid();
    }

    private void GenerateSingleUuid()
    {
        try
        {
            var uuid = Guid.NewGuid();
            _txtUuid.Text = FormatUuid(uuid);
            _lblStatus.Text = "Status: ✓ UUID oluşturuldu";
            _lblStatus.ForeColor = GreenText;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Status: ✗ Hata - {ex.Message}";
            _lblStatus.ForeColor = Color.Red;
        }
    }

    private void GenerateBulkUuids()
    {
        try
        {
            var count = (int)_numCount.Value;
            var result = new System.Text.StringBuilder();

            for (int i = 0; i < count; i++)
            {
                var uuid = Guid.NewGuid();
                result.AppendLine(FormatUuid(uuid));
            }

            _txtBulk.Text = result.ToString();
            _lblStatus.Text = $"Status: ✓ {count} UUID oluşturuldu";
            _lblStatus.ForeColor = GreenText;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Status: ✗ Hata - {ex.Message}";
            _lblStatus.ForeColor = Color.Red;
        }
    }

    private string FormatUuid(Guid uuid)
    {
        return _cmbFormat.SelectedIndex switch
        {
            0 => uuid.ToString("D"), // Standard with dashes
            1 => uuid.ToString("N"), // No dashes
            2 => uuid.ToString("D").ToUpperInvariant(), // Uppercase
            3 => uuid.ToString("B"), // Braces {}
            4 => uuid.ToString("P"), // Parentheses ()
            _ => uuid.ToString("D")
        };
    }

    private void CopyToClipboard(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Clipboard.SetText(text);
            MessageBox.Show("Panoya kopyalandı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }
}
