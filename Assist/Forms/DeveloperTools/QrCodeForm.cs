using System.Drawing.Imaging;

namespace Assist.Forms.DeveloperTools;

/// <summary>
/// QR Code generator using Google Charts API and simple decoder.
/// </summary>
internal sealed class QrCodeForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly HttpClient HttpClient = new();

    private readonly TextBox _txtInput = null!;
    private readonly PictureBox _pictureBox = null!;
    private readonly TextBox _txtDecoded = null!;
    private readonly Label _lblStatus = null!;

    public QrCodeForm()
    {
        Text = "QR Code Generator";
        ClientSize = new Size(800, 650);
        MinimumSize = new Size(650, 480);
        StartPosition = FormStartPosition.CenterParent;
        AutoScroll = true;
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== QR CODE GENERATOR ===",
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        var lblInput = new Label
        {
            Text = "Text/URL to Encode:",
            Location = new Point(20, 60),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtInput = new TextBox
        {
            Location = new Point(20, 85),
            Width = 760,
            Height = 60,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var btnGenerate = new Button
        {
            Text = "Generate QR Code",
            Location = new Point(20, 155),
            Width = 180,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnGenerate.FlatAppearance.BorderColor = GreenText;
        btnGenerate.Click += async (_, _) => await GenerateQrCodeAsync();

        var btnSave = new Button
        {
            Text = "Save QR Image",
            Location = new Point(210, 155),
            Width = 150,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnSave.FlatAppearance.BorderColor = GreenText;
        btnSave.Click += (_, _) => SaveQrCode();

        var btnCopy = new Button
        {
            Text = "Copy QR to Clipboard",
            Location = new Point(370, 155),
            Width = 200,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btnCopy.FlatAppearance.BorderColor = GreenText;
        btnCopy.Click += (_, _) => CopyQrToClipboard();

        _lblStatus = new Label
        {
            Text = "Status: Ready - Enter text and click Generate",
            Location = new Point(20, 195),
            Width = 760,
            Height = 20,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        _pictureBox = new PictureBox
        {
            Location = new Point(20, 225),
            Width = 350,
            Height = 350,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.White,
            SizeMode = PictureBoxSizeMode.CenterImage
        };

        var lblInfo = new Label
        {
            Text = "QR Code Info:",
            Location = new Point(390, 225),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtDecoded = new TextBox
        {
            Location = new Point(390, 250),
            Width = 390,
            Height = 325,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };

        ShowInfo();

        Controls.AddRange([lblTitle, lblInput, _txtInput, btnGenerate, btnSave, btnCopy, _lblStatus, _pictureBox, lblInfo, _txtDecoded]);
    }

    private void ShowInfo()
    {
        _txtDecoded.Text = """
            ╔════════════════════════════════════════╗
            ║         QR CODE GENERATOR              ║
            ╚════════════════════════════════════════╝

            Kullanım:
            ─────────────────────────────────────────
            1. Metin veya URL girin
            2. "Generate QR Code" tıklayın
            3. QR kodu kaydedin veya kopyalayın

            Özellikler:
            ─────────────────────────────────────────
            • URL encoding
            • Text encoding
            • PNG/JPEG kayıt
            • Clipboard kopyalama

            QR Code Size: 300x300 px
            Error Correction: Medium (M)
            """;
    }

    private async System.Threading.Tasks.Task GenerateQrCodeAsync()
    {
        try
        {
            if (string.IsNullOrWhiteSpace(_txtInput.Text))
            {
                _lblStatus.Text = "Status: Hata - Boş text";
                _lblStatus.ForeColor = Color.Yellow;
                return;
            }

            _lblStatus.Text = "Status: QR kod oluşturuluyor...";
            _lblStatus.ForeColor = Color.Yellow;

            var encodedText = Uri.EscapeDataString(_txtInput.Text);
            var url = $"https://api.qrserver.com/v1/create-qr-code/?size=300x300&data={encodedText}";

            var imageData = await HttpClient.GetByteArrayAsync(url);
            using var ms = new System.IO.MemoryStream(imageData);
            var bitmap = new Bitmap(ms);

            _pictureBox.Image = bitmap;

            _txtDecoded.Text = $"""
                ╔════════════════════════════════════════╗
                ║         QR CODE OLUŞTURULDU            ║
                ╚════════════════════════════════════════╝

                ✓ QR Kod başarıyla oluşturuldu!

                İçerik:
                ─────────────────────────────────────────
                {_txtInput.Text}

                Detaylar:
                ─────────────────────────────────────────
                Boyut: 300 x 300 piksel
                Format: PNG
                Karakter Sayısı: {_txtInput.Text.Length}
                Tarih: {DateTime.Now:dd.MM.yyyy HH:mm:ss}
                """;

            _lblStatus.Text = "Status: ✓ QR kod oluşturuldu";
            _lblStatus.ForeColor = GreenText;
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Status: ✗ Hata - {ex.Message}";
            _lblStatus.ForeColor = Color.Red;
        }
    }

    private void SaveQrCode()
    {
        try
        {
            if (_pictureBox.Image == null)
            {
                MessageBox.Show("Önce QR kod oluşturun!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var sfd = new SaveFileDialog
            {
                Filter = "PNG Image|*.png|JPEG Image|*.jpg|Bitmap Image|*.bmp",
                DefaultExt = "png",
                FileName = "qrcode.png"
            };

            if (sfd.ShowDialog() == DialogResult.OK)
            {
                var format = sfd.FilterIndex switch
                {
                    1 => ImageFormat.Png,
                    2 => ImageFormat.Jpeg,
                    3 => ImageFormat.Bmp,
                    _ => ImageFormat.Png
                };

                _pictureBox.Image.Save(sfd.FileName, format);
                _lblStatus.Text = $"Status: ✓ QR kod kaydedildi";
                _lblStatus.ForeColor = GreenText;
                MessageBox.Show($"QR kod kaydedildi:\n{sfd.FileName}", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Status: ✗ Kaydetme hatası";
            _lblStatus.ForeColor = Color.Red;
            MessageBox.Show($"Kaydetme hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void CopyQrToClipboard()
    {
        try
        {
            if (_pictureBox.Image == null)
            {
                MessageBox.Show("Önce QR kod oluşturun!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            Clipboard.SetImage(_pictureBox.Image);
            _lblStatus.Text = "Status: ✓ QR kod panoya kopyalandı";
            _lblStatus.ForeColor = GreenText;
            MessageBox.Show("QR kod panoya kopyalandı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Status: ✗ Kopyalama hatası";
            _lblStatus.ForeColor = Color.Red;
            MessageBox.Show($"Kopyalama hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
