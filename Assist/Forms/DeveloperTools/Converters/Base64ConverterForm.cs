namespace Assist.Forms.DeveloperTools.Converters;

using System.Text;

/// <summary>
/// Base64 encoder / decoder — text, file, and image preview support.
/// </summary>
internal sealed class Base64ConverterForm : Form
{

    private readonly TextBox _txtInput;
    private readonly TextBox _txtOutput;
    private readonly ComboBox _cmbEncoding;
    private readonly Label _lblStatus;
    private readonly PictureBox _picPreview;
    private readonly Panel _picPanel;
    private readonly Label _lblPicInfo;
    private MemoryStream? _imageStream;

    public Base64ConverterForm()
    {
        Text = "Base64 Encoder / Decoder";
        ClientSize = new Size(900, 720);
        MinimumSize = new Size(700, 500);
        StartPosition = FormStartPosition.CenterParent;
        AutoScroll = true;
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);

        int y = 12;

        var lblTitle = new Label
        {
            Text = "=== BASE64 ENCODER / DECODER ===",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        // Row 1: Encoding + main actions
        y = 44;
        var lblEnc = new Label
        {
            Text = "Enc:",
            Location = new Point(20, y + 3),
            AutoSize = true,
            ForeColor = AppConstants.AccentText
        };

        _cmbEncoding = new ComboBox
        {
            Location = new Point(72, y),
            Size = new Size(120, 24),
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 9)
        };
        _cmbEncoding.Items.AddRange(["UTF-8", "ASCII", "UTF-16", "Latin-1"]);
        _cmbEncoding.SelectedIndex = 0;

        var btnEncode = Btn("Encode →", 202, y, 115);
        btnEncode.Click += (_, _) => EncodeText();

        var btnDecode = Btn("← Decode", 323, y, 115);
        btnDecode.Click += (_, _) => DecodeText();

        var btnSwap = Btn("⇅ Swap", 444, y, 100);
        btnSwap.Click += (_, _) => SwapPanels();

        var btnCopy = Btn("Kopyala", 550, y, 110);
        btnCopy.Click += (_, _) => CopyOutput();

        var btnClear = Btn("Temizle", 666, y, 110);
        btnClear.Click += (_, _) => ClearAll();

        // Row 2: File & image actions
        y = 80;
        var btnFileEnc = Btn("Dosya → Base64", 20, y, 170);
        btnFileEnc.Click += (_, _) => EncodeFile();

        var btnFileDec = Btn("Base64 → Dosya", 198, y, 170);
        btnFileDec.Click += (_, _) => DecodeToFile();

        var btnImgPreview = Btn("Resim Önizle", 376, y, 150);
        btnImgPreview.Click += (_, _) => PreviewImage();

        // Input
        y = 116;
        var lblInput = new Label
        {
            Text = "Girdi (Normal metin veya Base64):",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        y += 22;
        _txtInput = new TextBox
        {
            Location = new Point(20, y),
            Size = new Size(860, 170),
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10),
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // Output
        y = 312;
        var lblOutput = new Label
        {
            Text = "Çıktı:",
            Location = new Point(20, y),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        y += 22;
        _txtOutput = new TextBox
        {
            Location = new Point(20, y),
            Size = new Size(860, 170),
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10),
            Multiline = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            ReadOnly = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        // Image preview panel
        y = 510;
        _picPanel = new Panel
        {
            Location = new Point(20, y),
            Size = new Size(860, 160),
            BackColor = Color.FromArgb(15, 15, 15),
            BorderStyle = BorderStyle.FixedSingle,
            Visible = false,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _lblPicInfo = new Label
        {
            Text = "",
            Dock = DockStyle.Bottom,
            Height = 22,
            ForeColor = Color.Cyan,
            BackColor = Color.FromArgb(15, 15, 15),
            Font = new Font("Consolas", 8),
            TextAlign = ContentAlignment.MiddleLeft
        };

        _picPreview = new PictureBox
        {
            Dock = DockStyle.Fill,
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.FromArgb(15, 15, 15)
        };

        _picPanel.Controls.Add(_picPreview);
        _picPanel.Controls.Add(_lblPicInfo);

        // Status
        _lblStatus = new Label
        {
            Text = "",
            Location = new Point(20, 690),
            Size = new Size(860, 20),
            ForeColor = Color.Cyan,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        Controls.AddRange([
            lblTitle, lblEnc, _cmbEncoding,
            btnEncode, btnDecode, btnSwap, btnCopy, btnClear,
            btnFileEnc, btnFileDec, btnImgPreview,
            lblInput, _txtInput,
            lblOutput, _txtOutput,
            _picPanel,
            _lblStatus
        ]);
    }

    private static Button Btn(string text, int x, int y, int w)
    {
        var btn = new Button
        {
            Text = text,
            Location = new Point(x, y),
            Size = new Size(w, 30),
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderColor = AppConstants.AccentText;
        return btn;
    }

    private Encoding GetSelectedEncoding() => _cmbEncoding.SelectedIndex switch
    {
        1 => Encoding.ASCII,
        2 => Encoding.Unicode,
        3 => Encoding.Latin1,
        _ => Encoding.UTF8
    };

    private void EncodeText()
    {
        if (string.IsNullOrEmpty(_txtInput.Text)) { Status("Girdi alanı boş.", Color.Orange); return; }

        try
        {
            HideImagePreview();
            var bytes = GetSelectedEncoding().GetBytes(_txtInput.Text);
            _txtOutput.Text = Convert.ToBase64String(bytes);
            Status($"Encode edildi — {bytes.Length} byte → {_txtOutput.Text.Length} karakter (Base64)", Color.Cyan);
        }
        catch (Exception ex) { Status($"Encode hatası: {ex.Message}", Color.Red); }
    }

    private void DecodeText()
    {
        if (string.IsNullOrEmpty(_txtInput.Text)) { Status("Girdi alanı boş.", Color.Orange); return; }

        try
        {
            var cleaned = StripDataUri(_txtInput.Text.Trim());
            var bytes = Convert.FromBase64String(cleaned);

            // Auto-detect image
            if (TryShowImagePreview(bytes))
            {
                _txtOutput.Text = $"[Resim algılandı — {bytes.Length:N0} byte]";
                Status($"Decode edildi — resim önizlemesi gösteriliyor ({bytes.Length:N0} byte)", Color.Cyan);
                return;
            }

            HideImagePreview();
            _txtOutput.Text = GetSelectedEncoding().GetString(bytes);
            Status($"Decode edildi — {cleaned.Length} karakter (Base64) → {bytes.Length} byte", Color.Cyan);
        }
        catch (FormatException) { Status("Geçersiz Base64 formatı. Girdiyi kontrol edin.", Color.Red); }
        catch (Exception ex) { Status($"Decode hatası: {ex.Message}", Color.Red); }
    }

    private void SwapPanels()
    {
        (_txtInput.Text, _txtOutput.Text) = (_txtOutput.Text, _txtInput.Text);
        Status("Girdi ve çıktı yer değiştirdi.", Color.Cyan);
    }

    private void ClearAll()
    {
        _txtInput.Clear();
        _txtOutput.Clear();
        _lblStatus.Text = "";
        HideImagePreview();
    }

    private void CopyOutput()
    {
        if (string.IsNullOrEmpty(_txtOutput.Text)) { Status("Kopyalanacak çıktı yok.", Color.Orange); return; }
        Clipboard.SetText(_txtOutput.Text);
        Status("Çıktı panoya kopyalandı.", Color.Cyan);
    }

    private void PreviewImage()
    {
        if (string.IsNullOrEmpty(_txtInput.Text)) { Status("Girdi alanına Base64 string yapıştırın.", Color.Orange); return; }

        try
        {
            var cleaned = StripDataUri(_txtInput.Text.Trim());
            var bytes = Convert.FromBase64String(cleaned);
            if (TryShowImagePreview(bytes))
                Status($"Resim önizlemesi — {bytes.Length:N0} byte", Color.Cyan);
            else
                Status("Bu Base64 verisi geçerli bir resim değil.", Color.Orange);
        }
        catch (FormatException) { Status("Geçersiz Base64 formatı.", Color.Red); }
    }

    private bool TryShowImagePreview(byte[] bytes)
    {
        try
        {
            // Keep stream alive — Image.FromStream requires the stream to remain open
            var ms = new MemoryStream(bytes);
            var img = Image.FromStream(ms, useEmbeddedColorManagement: false, validateImageData: true);

            _picPreview.Image?.Dispose();
            _imageStream?.Dispose();

            _imageStream = ms;
            _picPreview.Image = img;
            _lblPicInfo.Text = $"  {img.Width}×{img.Height} px  |  {bytes.Length:N0} byte";
            _picPanel.Visible = true;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private void HideImagePreview()
    {
        _picPanel.Visible = false;
        _picPreview.Image?.Dispose();
        _picPreview.Image = null;
        _imageStream?.Dispose();
        _imageStream = null;
    }

    /// <summary>
    /// Strips "data:image/png;base64," or similar data-URI prefix if present.
    /// </summary>
    private static string StripDataUri(string input)
    {
        const string marker = ";base64,";
        int idx = input.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return idx >= 0 ? input[(idx + marker.Length)..] : input;
    }

    private void EncodeFile()
    {
        using var ofd = new OpenFileDialog
        {
            Title = "Base64'e çevrilecek dosyayı seçin",
            Filter = "Tüm dosyalar (*.*)|*.*"
        };
        if (ofd.ShowDialog() != DialogResult.OK) return;

        try
        {
            var bytes = File.ReadAllBytes(ofd.FileName);
            _txtOutput.Text = Convert.ToBase64String(bytes);
            _txtInput.Text = $"[Dosya: {Path.GetFileName(ofd.FileName)} — {bytes.Length:N0} byte]";
            Status($"Dosya encode edildi — {bytes.Length:N0} byte → {_txtOutput.Text.Length:N0} karakter", Color.Cyan);
        }
        catch (Exception ex) { Status($"Dosya okuma hatası: {ex.Message}", Color.Red); }
    }

    private void DecodeToFile()
    {
        if (string.IsNullOrEmpty(_txtInput.Text)) { Status("Girdi alanına Base64 string yapıştırın.", Color.Orange); return; }

        using var sfd = new SaveFileDialog
        {
            Title = "Dosya olarak kaydet",
            Filter = "Tüm dosyalar (*.*)|*.*"
        };
        if (sfd.ShowDialog() != DialogResult.OK) return;

        try
        {
            var cleaned = StripDataUri(_txtInput.Text.Trim());
            var bytes = Convert.FromBase64String(cleaned);
            File.WriteAllBytes(sfd.FileName, bytes);
            _txtOutput.Text = $"[Dosya kaydedildi: {sfd.FileName}]";
            Status($"Dosyaya decode edildi — {bytes.Length:N0} byte kaydedildi.", Color.Cyan);
        }
        catch (FormatException) { Status("Geçersiz Base64 formatı.", Color.Red); }
        catch (Exception ex) { Status($"Dosya kaydetme hatası: {ex.Message}", Color.Red); }
    }

    private void Status(string text, Color color)
    {
        _lblStatus.ForeColor = color;
        _lblStatus.Text = text;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _picPreview.Image?.Dispose();
            _imageStream?.Dispose();
        }
        base.Dispose(disposing);
    }
}
