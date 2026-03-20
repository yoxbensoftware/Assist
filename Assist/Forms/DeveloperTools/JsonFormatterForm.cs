using System.Text.Json;

namespace Assist;

/// <summary>
/// JSON formatter and validator tool.
/// </summary>
public sealed class JsonFormatterForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);

    // Cache JsonSerializerOptions instances (CA1869)
    private static readonly JsonSerializerOptions FormattedOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions MinifiedOptions = new() { WriteIndented = false };

    private readonly TextBox _txtInput = null!;
    private readonly TextBox _txtOutput = null!;
    private readonly Button _btnFormat = null!;
    private readonly Button _btnValidate = null!;
    private readonly Button _btnMinify = null!;
    private readonly Button _btnClear = null!;
    private readonly Label _lblStatus = null!;

    public JsonFormatterForm()
    {
        Text = "JSON Formatter";
        ClientSize = new Size(900, 600);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== JSON FORMATTER & VALIDATOR ===",
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        var lblInput = new Label
        {
            Text = "JSON Girişi:",
            Location = new Point(20, 60),
            AutoSize = true,
            ForeColor = GreenText
        };

        _txtInput = new TextBox
        {
            Location = new Point(20, 85),
            Width = 860,
            Height = 200,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle
        };

        var buttonPanel = new Panel
        {
            Location = new Point(20, 300),
            Width = 860,
            Height = 45,
            BackColor = Color.Black
        };

        _btnFormat = CreateButton("Format (Pretty)", 0);
        _btnValidate = CreateButton("Validate", 210);
        _btnMinify = CreateButton("Minify", 420);
        _btnClear = CreateButton("Temizle", 630);

        _btnFormat.Click += (_, _) => FormatJson();
        _btnValidate.Click += (_, _) => ValidateJson();
        _btnMinify.Click += (_, _) => MinifyJson();
        _btnClear.Click += (_, _) => ClearAll();

        buttonPanel.Controls.AddRange(new Control[] { _btnFormat, _btnValidate, _btnMinify, _btnClear });

        _lblStatus = new Label
        {
            Location = new Point(20, 355),
            Width = 860,
            Height = 25,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10),
            Text = "Hazır."
        };

        var lblOutput = new Label
        {
            Text = "JSON Çıkışı:",
            Location = new Point(20, 390),
            AutoSize = true,
            ForeColor = GreenText
        };

        _txtOutput = new TextBox
        {
            Location = new Point(20, 415),
            Width = 860,
            Height = 165,
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true
        };

        Controls.AddRange(new Control[]
        {
            lblTitle, lblInput, _txtInput, buttonPanel,
            _lblStatus, lblOutput, _txtOutput
        });

        // Sample JSON
        _txtInput.Text = @"{""name"":""John"",""age"":30,""city"":""Istanbul""}";
    }

    private Button CreateButton(string text, int x)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, 0),
            Width = 200,
            Height = 40,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 10)
        };
        btn.FlatAppearance.BorderColor = GreenText;
        return btn;
    }

    private void FormatJson()
    {
        try
        {
            var input = _txtInput.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                SetStatus("JSON girişi boş.", Color.Yellow);
                return;
            }

            var jsonElement = JsonSerializer.Deserialize<JsonElement>(input);
            var formatted = JsonSerializer.Serialize(jsonElement, FormattedOptions);

            _txtOutput.Text = formatted;
            SetStatus("✓ JSON başarıyla formatlandı.", Color.Lime);
        }
        catch (JsonException ex)
        {
            _txtOutput.Text = string.Empty;
            SetStatus($"✗ Geçersiz JSON: {ex.Message}", Color.Red);
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Hata: {ex.Message}", Color.Red);
        }
    }

    private void ValidateJson()
    {
        try
        {
            var input = _txtInput.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                SetStatus("JSON girişi boş.", Color.Yellow);
                return;
            }

            JsonSerializer.Deserialize<JsonElement>(input);
            SetStatus("✓ JSON geçerli!", Color.Lime);
        }
        catch (JsonException ex)
        {
            SetStatus($"✗ Geçersiz JSON: {ex.Message}", Color.Red);
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Hata: {ex.Message}", Color.Red);
        }
    }

    private void MinifyJson()
    {
        try
        {
            var input = _txtInput.Text.Trim();
            if (string.IsNullOrEmpty(input))
            {
                SetStatus("JSON girişi boş.", Color.Yellow);
                return;
            }

            var jsonElement = JsonSerializer.Deserialize<JsonElement>(input);
            var minified = JsonSerializer.Serialize(jsonElement, MinifiedOptions);

            _txtOutput.Text = minified;
            SetStatus("✓ JSON küçültüldü (minified).", Color.Lime);
        }
        catch (JsonException ex)
        {
            _txtOutput.Text = string.Empty;
            SetStatus($"✗ Geçersiz JSON: {ex.Message}", Color.Red);
        }
        catch (Exception ex)
        {
            SetStatus($"✗ Hata: {ex.Message}", Color.Red);
        }
    }

    private void ClearAll()
    {
        _txtInput.Clear();
        _txtOutput.Clear();
        SetStatus("Temizlendi.", GreenText);
    }

    private void SetStatus(string message, Color color)
    {
        _lblStatus.Text = message;
        _lblStatus.ForeColor = color;
    }
}
