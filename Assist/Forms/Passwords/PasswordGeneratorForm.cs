using System.Security.Cryptography;
using System.Text;

namespace Assist;

/// <summary>
/// Complex password generator with strength indicator.
/// </summary>
public sealed class PasswordGeneratorForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly string UpperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
    private static readonly string LowerCase = "abcdefghijklmnopqrstuvwxyz";
    private static readonly string Digits = "0123456789";
    private static readonly string Special = "!@#$%^&*()_+-=[]{}|;:,.<>?";

    private readonly TextBox _txtPassword = null!;
    private readonly TrackBar _lengthSlider = null!;
    private readonly Label _lengthLabel = null!;
    private readonly CheckBox _chkUpper = null!;
    private readonly CheckBox _chkLower = null!;
    private readonly CheckBox _chkDigits = null!;
    private readonly CheckBox _chkSpecial = null!;
    private readonly Label _strengthLabel = null!;
    private readonly Button _btnGenerate = null!;
    private readonly Button _btnCopy = null!;

    public PasswordGeneratorForm()
    {
        Text = "Password Generator";
        ClientSize = new Size(500, 350);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== PASSWORD GENERATOR ===",
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        _txtPassword = new TextBox
        {
            Location = new Point(20, 60),
            Width = 460,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 14),
            ReadOnly = true,
            BorderStyle = BorderStyle.FixedSingle
        };

        _lengthLabel = new Label
        {
            Text = "Uzunluk: 16",
            Location = new Point(20, 110),
            AutoSize = true,
            ForeColor = GreenText
        };

        _lengthSlider = new TrackBar
        {
            Location = new Point(20, 135),
            Width = 460,
            Minimum = 8,
            Maximum = 64,
            Value = 16,
            TickFrequency = 4
        };
        _lengthSlider.ValueChanged += (_, _) => _lengthLabel.Text = $"Uzunluk: {_lengthSlider.Value}";

        _chkUpper = new CheckBox
        {
            Text = "Büyük Harf (A-Z)",
            Location = new Point(20, 190),
            AutoSize = true,
            ForeColor = GreenText,
            Checked = true
        };

        _chkLower = new CheckBox
        {
            Text = "Küçük Harf (a-z)",
            Location = new Point(200, 190),
            AutoSize = true,
            ForeColor = GreenText,
            Checked = true
        };

        _chkDigits = new CheckBox
        {
            Text = "Rakam (0-9)",
            Location = new Point(20, 220),
            AutoSize = true,
            ForeColor = GreenText,
            Checked = true
        };

        _chkSpecial = new CheckBox
        {
            Text = "Özel Karakter (!@#$...)",
            Location = new Point(200, 220),
            AutoSize = true,
            ForeColor = GreenText,
            Checked = true
        };

        _strengthLabel = new Label
        {
            Text = "Güç: -",
            Location = new Point(20, 260),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 11, FontStyle.Bold)
        };

        _btnGenerate = new Button
        {
            Text = "Şifre Üret",
            Location = new Point(20, 290),
            Width = 220,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat
        };
        _btnGenerate.FlatAppearance.BorderColor = GreenText;
        _btnGenerate.Click += (_, _) => GeneratePassword();

        _btnCopy = new Button
        {
            Text = "Kopyala",
            Location = new Point(260, 290),
            Width = 220,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Enabled = false
        };
        _btnCopy.FlatAppearance.BorderColor = GreenText;
        _btnCopy.Click += (_, _) => CopyPassword();

        Controls.AddRange(new Control[]
        {
            lblTitle, _txtPassword, _lengthLabel, _lengthSlider,
            _chkUpper, _chkLower, _chkDigits, _chkSpecial,
            _strengthLabel, _btnGenerate, _btnCopy
        });

        // Generate initial password
        GeneratePassword();
    }

    private void GeneratePassword()
    {
        var charset = new StringBuilder();
        if (_chkUpper.Checked) charset.Append(UpperCase);
        if (_chkLower.Checked) charset.Append(LowerCase);
        if (_chkDigits.Checked) charset.Append(Digits);
        if (_chkSpecial.Checked) charset.Append(Special);

        if (charset.Length == 0)
        {
            MessageBox.Show("En az bir karakter tipi seçmelisiniz!", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var length = _lengthSlider.Value;
        var password = new StringBuilder(length);
        var charsetStr = charset.ToString();

        using var rng = RandomNumberGenerator.Create();
        var buffer = new byte[length];
        rng.GetBytes(buffer);

        for (int i = 0; i < length; i++)
        {
            password.Append(charsetStr[buffer[i] % charsetStr.Length]);
        }

        _txtPassword.Text = password.ToString();
        _btnCopy.Enabled = true;
        UpdateStrength();
    }

    private void UpdateStrength()
    {
        var pwd = _txtPassword.Text;
        if (string.IsNullOrEmpty(pwd))
        {
            _strengthLabel.Text = "Güç: -";
            _strengthLabel.ForeColor = GreenText;
            return;
        }

        var score = 0;
        if (pwd.Length >= 12) score++;
        if (pwd.Length >= 16) score++;
        if (pwd.Length >= 20) score++;
        if (pwd.Any(char.IsUpper)) score++;
        if (pwd.Any(char.IsLower)) score++;
        if (pwd.Any(char.IsDigit)) score++;
        if (pwd.Any(c => Special.Contains(c))) score++;

        if (score >= 6)
        {
            _strengthLabel.Text = "Güç: ████████ (ÇOK GÜÇLÜ)";
            _strengthLabel.ForeColor = Color.Lime;
        }
        else if (score >= 4)
        {
            _strengthLabel.Text = "Güç: ██████░░ (GÜÇLÜ)";
            _strengthLabel.ForeColor = GreenText;
        }
        else if (score >= 2)
        {
            _strengthLabel.Text = "Güç: ████░░░░ (ORTA)";
            _strengthLabel.ForeColor = Color.Yellow;
        }
        else
        {
            _strengthLabel.Text = "Güç: ██░░░░░░ (ZAYIF)";
            _strengthLabel.ForeColor = Color.Red;
        }
    }

    private void CopyPassword()
    {
        if (string.IsNullOrEmpty(_txtPassword.Text)) return;

        try
        {
            Clipboard.SetText(_txtPassword.Text);
            MessageBox.Show("Şifre panoya kopyalandı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Kopyalama hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
