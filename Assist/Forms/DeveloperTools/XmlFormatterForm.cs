namespace Assist.Forms.DeveloperTools;

using System.Xml;
using System.Xml.Linq;

/// <summary>
/// XML formatter and validator with pretty print support.
/// </summary>
internal sealed class XmlFormatterForm : Form
{

    private readonly TextBox _txtInput = null!;
    private readonly TextBox _txtOutput = null!;
    private readonly Label _lblStatus = null!;

    public XmlFormatterForm()
    {
        Text = "XML Formatter";
        ClientSize = new Size(1000, 600);
        MinimumSize = new Size(700, 500);
        StartPosition = FormStartPosition.CenterParent;
        AutoScroll = true;
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== XML FORMATTER ===",
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        var lblInput = new Label
        {
            Text = "XML Girişi:",
            Location = new Point(20, 60),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtInput = new TextBox
        {
            Location = new Point(20, 85),
            Width = 960,
            Height = 200,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var btnFormat = new Button
        {
            Text = "Format (Pretty Print)",
            Location = new Point(20, 295),
            Width = 200,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnFormat.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnFormat.Click += (_, _) => FormatXml();

        var btnValidate = new Button
        {
            Text = "Validate",
            Location = new Point(230, 295),
            Width = 150,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnValidate.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnValidate.Click += (_, _) => ValidateXml();

        var btnMinify = new Button
        {
            Text = "Minify",
            Location = new Point(390, 295),
            Width = 150,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnMinify.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnMinify.Click += (_, _) => MinifyXml();

        var btnClear = new Button
        {
            Text = "Clear",
            Location = new Point(550, 295),
            Width = 100,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnClear.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnClear.Click += (_, _) =>
        {
            _txtInput?.Clear();
            _txtOutput?.Clear();
            if (_lblStatus is not null)
                _lblStatus.Text = "Status: Ready";
        };

        _lblStatus = new Label
        {
            Text = "Status: Ready",
            Location = new Point(20, 335),
            Width = 960,
            Height = 20,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var lblOutput = new Label
        {
            Text = "XML Çıktısı:",
            Location = new Point(20, 365),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtOutput = new TextBox
        {
            Location = new Point(20, 390),
            Width = 960,
            Height = 190,
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

        Controls.AddRange([lblTitle, lblInput, _txtInput, btnFormat, btnValidate, btnMinify, btnClear, _lblStatus, lblOutput, _txtOutput]);
    }

    private void FormatXml()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_txtInput.Text))
            {
                _lblStatus.Text = "Status: Hata - Boş XML";
                _lblStatus.ForeColor = Color.Yellow;
                return;
            }

            var doc = XDocument.Parse(_txtInput.Text);
            _txtOutput.Text = doc.ToString();
            _lblStatus.Text = "Status: ✓ XML başarıyla formatlandı";
            _lblStatus.ForeColor = AppConstants.AccentText;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Status: ✗ Format Hatası - {ex.Message}";
            _lblStatus.ForeColor = Color.Red;
        }
    }

    private void ValidateXml()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_txtInput.Text))
            {
                _lblStatus.Text = "Status: Hata - Boş XML";
                _lblStatus.ForeColor = Color.Yellow;
                return;
            }

            var doc = new XmlDocument();
            doc.LoadXml(_txtInput.Text);

            _lblStatus.Text = "Status: ✓ XML geçerli (Valid)";
            _lblStatus.ForeColor = AppConstants.AccentText;
            _txtOutput.Text = "XML syntax'ı doğru.";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Status: ✗ Geçersiz XML - {ex.Message}";
            _lblStatus.ForeColor = Color.Red;
            _txtOutput.Text = ex.Message;
        }
    }

    private void MinifyXml()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_txtInput.Text))
            {
                _lblStatus.Text = "Status: Hata - Boş XML";
                _lblStatus.ForeColor = Color.Yellow;
                return;
            }

            var doc = XDocument.Parse(_txtInput.Text);
            _txtOutput.Text = doc.ToString(SaveOptions.DisableFormatting);
            _lblStatus.Text = "Status: ✓ XML minify edildi";
            _lblStatus.ForeColor = AppConstants.AccentText;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Status: ✗ Minify Hatası - {ex.Message}";
            _lblStatus.ForeColor = Color.Red;
        }
    }
}
