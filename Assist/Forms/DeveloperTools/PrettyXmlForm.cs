using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Assist.Forms.DeveloperTools;

/// <summary>
/// Pretty XML — format, minify, validate XML. Supports raw XML and Base64-encoded XML input.
/// </summary>
internal sealed class PrettyXmlForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);

    private readonly TextBox _txtInput;
    private readonly TextBox _txtOutput;
    private readonly Label _lblStatus;

    public PrettyXmlForm()
    {
        Text = "Pretty XML";
        ClientSize = new Size(860, 620);
        MinimumSize = new Size(700, 500);
        StartPosition = FormStartPosition.CenterParent;
        AutoScroll = true;
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        int y = 12;
        var lblTitle = new Label
        {
            Text = "=== PRETTY XML ===",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        // Buttons
        y = 44;
        var btnPretty = Btn("Pretty Print", 20, y, 120);
        btnPretty.Click += (_, _) => FormatPretty();

        var btnMinify = Btn("Minify", 145, y, 80);
        btnMinify.Click += (_, _) => Minify();

        var btnValidate = Btn("Validate", 230, y, 80);
        btnValidate.Click += (_, _) => Validate();

        var btnDecodeXml = Btn("Base64 → XML", 315, y, 130);
        btnDecodeXml.Click += (_, _) => DecodeBase64Xml();

        var btnEncodeXml = Btn("XML → Base64", 450, y, 130);
        btnEncodeXml.Click += (_, _) => EncodeXmlBase64();

        var btnCopy = Btn("Kopyala", 585, y, 85);
        btnCopy.Click += (_, _) => CopyOutput();

        var btnClear = Btn("Temizle", 675, y, 85);
        btnClear.Click += (_, _) => { _txtInput.Clear(); _txtOutput.Clear(); Status("Hazır.", GreenText); };

        var btnLoad = Btn("Dosya Aç", 765, y, 80);
        btnLoad.Click += (_, _) => LoadFile();

        // Input
        y = 78;
        var lblInput = new Label
        {
            Text = "XML Girişi (raw veya Base64):",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        y += 22;
        _txtInput = new TextBox
        {
            Location = new Point(20, y),
            Size = new Size(820, 210),
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // Output
        y = 318;
        var lblOutput = new Label
        {
            Text = "Çıktı:",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        y += 22;
        _txtOutput = new TextBox
        {
            Location = new Point(20, y),
            Size = new Size(820, 240),
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            ReadOnly = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        // Status
        _lblStatus = new Label
        {
            Text = "Hazır.",
            Location = new Point(20, 590),
            Size = new Size(820, 20),
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        Controls.AddRange([
            lblTitle,
            btnPretty, btnMinify, btnValidate, btnDecodeXml, btnEncodeXml, btnCopy, btnClear, btnLoad,
            lblInput, _txtInput,
            lblOutput, _txtOutput,
            _lblStatus
        ]);
    }

    private static Button Btn(string text, int x, int y, int w) => new()
    {
        Text = text,
        Location = new Point(x, y),
        Size = new Size(w, 28),
        BackColor = Color.FromArgb(30, 30, 30),
        ForeColor = GreenText,
        FlatStyle = FlatStyle.Flat,
        Font = new Font("Consolas", 9),
        Cursor = Cursors.Hand
    };

    /// <summary>
    /// Resolves the XML text from input — auto-detects Base64 encoding.
    /// </summary>
    private string? ResolveXml()
    {
        if (string.IsNullOrWhiteSpace(_txtInput.Text))
        {
            Status("Girdi alanı boş.", Color.Orange);
            return null;
        }

        var raw = _txtInput.Text.Trim();

        // Try Base64 first
        try
        {
            var bytes = Convert.FromBase64String(raw);
            var decoded = Encoding.UTF8.GetString(bytes);
            if (decoded.TrimStart().StartsWith('<'))
                return decoded;
        }
        catch (FormatException) { }

        return raw;
    }

    private void FormatPretty()
    {
        var xml = ResolveXml();
        if (xml is null) return;

        try
        {
            var doc = XDocument.Parse(xml);
            _txtOutput.Text = doc.ToString();
            Status("✓ XML güzelleştirildi (Pretty Print).", GreenText);
        }
        catch (Exception ex) { Status($"✗ XML parse hatası: {ex.Message}", Color.Red); }
    }

    private void Minify()
    {
        var xml = ResolveXml();
        if (xml is null) return;

        try
        {
            var doc = XDocument.Parse(xml);
            _txtOutput.Text = doc.ToString(SaveOptions.DisableFormatting);
            Status("✓ XML minify edildi.", GreenText);
        }
        catch (Exception ex) { Status($"✗ XML parse hatası: {ex.Message}", Color.Red); }
    }

    private void Validate()
    {
        var xml = ResolveXml();
        if (xml is null) return;

        try
        {
            var doc = new XmlDocument();
            doc.LoadXml(xml);

            int elemCount = doc.SelectNodes("//*")?.Count ?? 0;
            int attrCount = doc.SelectNodes("//@*")?.Count ?? 0;

            _txtOutput.Text = $"XML geçerli (Valid).\n\nElement sayısı: {elemCount}\nAttribute sayısı: {attrCount}\nRoot element: <{doc.DocumentElement?.Name}>";
            Status("✓ XML geçerli (Valid).", GreenText);
        }
        catch (Exception ex)
        {
            _txtOutput.Text = ex.Message;
            Status($"✗ Geçersiz XML: {ex.Message}", Color.Red);
        }
    }

    private void DecodeBase64Xml()
    {
        if (string.IsNullOrWhiteSpace(_txtInput.Text)) { Status("Girdi alanı boş.", Color.Orange); return; }

        try
        {
            var bytes = Convert.FromBase64String(_txtInput.Text.Trim());
            var decoded = Encoding.UTF8.GetString(bytes);
            var doc = XDocument.Parse(decoded);
            _txtOutput.Text = doc.ToString();
            Status($"✓ Base64 → XML decode + Pretty Print edildi ({bytes.Length:N0} byte).", GreenText);
        }
        catch (FormatException) { Status("Geçersiz Base64 formatı.", Color.Red); }
        catch (Exception ex) { Status($"✗ Hata: {ex.Message}", Color.Red); }
    }

    private void EncodeXmlBase64()
    {
        if (string.IsNullOrWhiteSpace(_txtInput.Text)) { Status("Girdi alanı boş.", Color.Orange); return; }

        try
        {
            // Validate it's XML first
            XDocument.Parse(_txtInput.Text);
            var bytes = Encoding.UTF8.GetBytes(_txtInput.Text);
            _txtOutput.Text = Convert.ToBase64String(bytes);
            Status($"✓ XML → Base64 encode edildi ({bytes.Length:N0} byte → {_txtOutput.Text.Length:N0} karakter).", GreenText);
        }
        catch (Exception ex) { Status($"✗ Hata: {ex.Message}", Color.Red); }
    }

    private void CopyOutput()
    {
        if (string.IsNullOrEmpty(_txtOutput.Text)) { Status("Kopyalanacak çıktı yok.", Color.Orange); return; }
        Clipboard.SetText(_txtOutput.Text);
        Status("Çıktı panoya kopyalandı.", Color.Cyan);
    }

    private void LoadFile()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "XML dosyasını seçin",
            Filter = "XML dosyaları (*.xml)|*.xml|Tüm dosyalar (*.*)|*.*"
        };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        try
        {
            _txtInput.Text = File.ReadAllText(ofd.FileName);
            Status($"Dosya yüklendi: {Path.GetFileName(ofd.FileName)}", Color.Cyan);
        }
        catch (Exception ex) { Status($"Dosya okuma hatası: {ex.Message}", Color.Red); }
    }

    private void Status(string text, Color color)
    {
        _lblStatus.ForeColor = color;
        _lblStatus.Text = text;
    }
}
