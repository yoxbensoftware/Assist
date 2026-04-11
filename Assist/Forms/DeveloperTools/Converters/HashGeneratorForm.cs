namespace Assist.Forms.DeveloperTools.Converters;

using System.Security.Cryptography;
using System.Text;

/// <summary>
/// Hash generator — MD5, SHA-1, SHA-256, SHA-512 for text and files.
/// </summary>
internal sealed class HashGeneratorForm : Form
{

    private readonly TextBox _txtInput = null!;
    private readonly TextBox _txtMd5 = null!;
    private readonly TextBox _txtSha1 = null!;
    private readonly TextBox _txtSha256 = null!;
    private readonly TextBox _txtSha512 = null!;
    private readonly TextBox _txtVerify = null!;
    private readonly Label _lblVerifyResult = null!;
    private readonly Label _lblStatus = null!;

    public HashGeneratorForm()
    {
        Text = "Hash Generator";
        ClientSize = new Size(800, 620);
        MinimumSize = new Size(650, 480);
        StartPosition = FormStartPosition.CenterParent;
        AutoScroll = true;
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== HASH GENERATOR ===",
            Location = new Point(20, 15),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        // Input
        var lblInput = new Label
        {
            Text = "Metin Girişi:",
            Location = new Point(20, 50),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtInput = new TextBox
        {
            Location = new Point(20, 75),
            Size = new Size(760, 80),
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10),
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _txtInput.TextChanged += (_, _) => ComputeTextHashes();

        var btnFile = CreateButton("📁 Dosya Seç", new Point(20, 165));
        btnFile.Click += (_, _) => SelectFileAndHash();

        var btnClear = CreateButton("🗑️ Temizle", new Point(200, 165));
        btnClear.Click += (_, _) => ClearAll();

        // Hash results
        var y = 210;
        AddHashRow("MD5:", ref y, out _txtMd5);
        AddHashRow("SHA-1:", ref y, out _txtSha1);
        AddHashRow("SHA-256:", ref y, out _txtSha256);
        AddHashRow("SHA-512:", ref y, out _txtSha512);

        // Verification
        var lblVerify = new Label
        {
            Text = "Doğrulama (hash yapıştır):",
            Location = new Point(20, y + 10),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtVerify = new TextBox
        {
            Location = new Point(20, y + 35),
            Size = new Size(660, 25),
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };
        _txtVerify.TextChanged += (_, _) => VerifyHash();

        _lblVerifyResult = new Label
        {
            Text = "",
            Location = new Point(690, y + 37),
            Width = 90,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _lblStatus = new Label
        {
            Text = "Metin girin veya dosya seçin.",
            Location = new Point(20, y + 70),
            Width = 760,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        Controls.AddRange([
            lblTitle, lblInput, _txtInput, btnFile, btnClear,
            lblVerify, _txtVerify, _lblVerifyResult, _lblStatus
        ]);
    }

    private void AddHashRow(string label, ref int y, out TextBox textBox)
    {
        var lbl = new Label
        {
            Text = label,
            Location = new Point(20, y),
            Width = 80,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        textBox = new TextBox
        {
            Location = new Point(105, y - 2),
            Width = 630,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 9),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var btnCopy = new Button
        {
            Text = "📋",
            Location = new Point(745, y - 4),
            Width = 35,
            Height = 25,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Right
        };
        btnCopy.FlatAppearance.BorderColor = AppConstants.AccentText;
        var tb = textBox;
        btnCopy.Click += (_, _) =>
        {
            if (!string.IsNullOrEmpty(tb.Text))
            {
                Clipboard.SetText(tb.Text);
                _lblStatus.Text = $"{label} kopyalandı!";
            }
        };

        Controls.AddRange([lbl, textBox, btnCopy]);
        y += 40;
    }

    private void ComputeTextHashes()
    {
        var text = _txtInput.Text;
        if (string.IsNullOrEmpty(text))
        {
            ClearHashOutputs();
            return;
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        SetHashResults(bytes);
        _lblStatus.Text = $"Metin hash'lendi ({bytes.Length} byte)";
    }

    private void SelectFileAndHash()
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Hash hesaplanacak dosyayı seçin",
            Filter = "Tüm Dosyalar (*.*)|*.*"
        };

        if (dialog.ShowDialog() != DialogResult.OK) return;

        try
        {
            var bytes = File.ReadAllBytes(dialog.FileName);
            _txtInput.Text = $"[Dosya: {dialog.FileName}]";
            SetHashResults(bytes);
            _lblStatus.Text = $"Dosya hash'lendi: {dialog.FileName} ({FormatSize(bytes.Length)})";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Dosya okuma hatası: {ex.Message}";
        }
    }

    private void SetHashResults(byte[] data)
    {
        _txtMd5.Text = ComputeHash(MD5.Create(), data);
        _txtSha1.Text = ComputeHash(SHA1.Create(), data);
        _txtSha256.Text = ComputeHash(SHA256.Create(), data);
        _txtSha512.Text = ComputeHash(SHA512.Create(), data);
        VerifyHash();
    }

    private static string ComputeHash(HashAlgorithm algorithm, byte[] data)
    {
        using (algorithm)
        {
            var hash = algorithm.ComputeHash(data);
            return Convert.ToHexStringLower(hash);
        }
    }

    private void VerifyHash()
    {
        var input = _txtVerify.Text.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(input))
        {
            _lblVerifyResult.Text = "";
            return;
        }

        var match = input == _txtMd5.Text || input == _txtSha1.Text ||
                    input == _txtSha256.Text || input == _txtSha512.Text;

        _lblVerifyResult.Text = match ? "✅ Eşleşti" : "❌ Eşleşmedi";
        _lblVerifyResult.ForeColor = match ? AppConstants.AccentText : Color.OrangeRed;
    }

    private void ClearAll()
    {
        _txtInput.Clear();
        _txtVerify.Clear();
        ClearHashOutputs();
        _lblStatus.Text = "Temizlendi.";
    }

    private void ClearHashOutputs()
    {
        _txtMd5.Clear();
        _txtSha1.Clear();
        _txtSha256.Clear();
        _txtSha512.Clear();
        _lblVerifyResult.Text = "";
    }

    private static string FormatSize(long bytes) => bytes switch
    {
        >= 1_073_741_824 => $"{bytes / 1_073_741_824.0:F2} GB",
        >= 1_048_576 => $"{bytes / 1_048_576.0:F2} MB",
        >= 1_024 => $"{bytes / 1_024.0:F2} KB",
        _ => $"{bytes} B"
    };

    private static Button CreateButton(string text, Point location)
    {
        var btn = new Button
        {
            Text = text,
            Location = location,
            Width = 160,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btn.FlatAppearance.BorderColor = AppConstants.AccentText;
        return btn;
    }
}
