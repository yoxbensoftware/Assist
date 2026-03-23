using Assist.Services;

namespace Assist.Forms.Core;

internal sealed class SetupWizardForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly Color DarkBg = Color.FromArgb(15, 15, 15);
    private static readonly Color PanelBg = Color.FromArgb(25, 25, 25);

    private readonly Panel _welcomePanel = new();
    private readonly Panel _credentialsPanel = new();

    private readonly TextBox _txtUsername = new();
    private readonly TextBox _txtPassword = new();
    private readonly TextBox _txtPasswordConfirm = new();
    private readonly Label _lblError = new();

    public bool SetupCompleted { get; private set; }

    public SetupWizardForm()
    {
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "🔒 Assist — Kurulum Sihirbazı";
        Size = new Size(620, 600);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = DarkBg;
        ForeColor = GreenText;
        ShowIcon = false;

        BuildWelcomePanel();
        BuildCredentialsPanel();

        _credentialsPanel.Visible = false;

        Controls.AddRange([_welcomePanel, _credentialsPanel]);
    }

    private void BuildWelcomePanel()
    {
        _welcomePanel.Dock = DockStyle.Fill;
        _welcomePanel.BackColor = DarkBg;
        _welcomePanel.Padding = new Padding(30);

        var lblIcon = new Label
        {
            Text = "🔒",
            Font = new Font("Segoe UI Emoji", 44f),
            AutoSize = true,
            ForeColor = GreenText,
            Location = new Point(250, 15)
        };

        var lblTitle = new Label
        {
            Text = "Assist",
            Font = new Font("Consolas", 36f, FontStyle.Bold),
            ForeColor = GreenText,
            AutoSize = true,
            Location = new Point(205, 100)
        };

        var lblVersion = new Label
        {
            Text = AppConstants.BuildVersion,
            Font = new Font("Consolas", 12f),
            ForeColor = Color.FromArgb(0, 180, 0),
            AutoSize = true,
            Location = new Point(260, 155)
        };

        var separator1 = new Panel
        {
            BackColor = Color.FromArgb(0, 100, 0),
            Size = new Size(540, 1),
            Location = new Point(30, 185)
        };

        var lblWelcome = new Label
        {
            Text = "Hoş Geldiniz!",
            Font = new Font("Consolas", 18f, FontStyle.Bold),
            ForeColor = GreenText,
            AutoSize = true,
            Location = new Point(190, 205)
        };

        var lblDesc = new Label
        {
            Text = "Assist, günlük iş akışınızı hızlandırmak\niçin tasarlanmış çok amaçlı bir masaüstü\naracıdır.\n\nKuruluma devam etmek için aşağıdaki\nbutona tıklayın. Bir sonraki adımda\ngiriş bilgilerinizi oluşturacaksınız.",
            Font = new Font("Consolas", 10.5f),
            ForeColor = Color.FromArgb(180, 220, 180),
            AutoSize = true,
            Location = new Point(40, 255)
        };

        var separator2 = new Panel
        {
            BackColor = Color.FromArgb(0, 100, 0),
            Size = new Size(540, 1),
            Location = new Point(30, 470)
        };

        var lblDev = new Label
        {
            Text = "Geliştirici: Oz",
            Font = new Font("Consolas", 10f, FontStyle.Italic),
            ForeColor = Color.FromArgb(0, 140, 0),
            AutoSize = true,
            Location = new Point(40, 490)
        };

        var btnNext = new Button
        {
            Text = "İleri  ▶",
            Font = new Font("Consolas", 13f, FontStyle.Bold),
            Size = new Size(180, 46),
            Location = new Point(390, 482),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(20, 60, 20),
            ForeColor = GreenText,
            Cursor = Cursors.Hand
        };
        btnNext.FlatAppearance.BorderColor = GreenText;
        btnNext.Click += (_, _) => ShowStep(1);

        _welcomePanel.Controls.AddRange([lblIcon, lblTitle, lblVersion, separator1, lblWelcome, lblDesc, separator2, lblDev, btnNext]);
    }

    private void BuildCredentialsPanel()
    {
        _credentialsPanel.Dock = DockStyle.Fill;
        _credentialsPanel.BackColor = DarkBg;

        var lblHeader = new Label
        {
            Text = "🔑  Hesap Oluşturma",
            Font = new Font("Consolas", 20f, FontStyle.Bold),
            ForeColor = GreenText,
            AutoSize = true,
            Location = new Point(40, 30)
        };

        var lblSubHeader = new Label
        {
            Text = "Uygulamaya giriş yapmak için kullanacağınız\nkullanıcı adı ve şifrenizi belirleyin.",
            Font = new Font("Consolas", 10.5f),
            ForeColor = Color.FromArgb(180, 220, 180),
            AutoSize = true,
            Location = new Point(40, 80)
        };

        var separator = new Panel
        {
            BackColor = Color.FromArgb(0, 100, 0),
            Size = new Size(540, 1),
            Location = new Point(30, 135)
        };

        var lblUser = new Label
        {
            Text = "Kullanıcı Adı:",
            Font = new Font("Consolas", 12f),
            ForeColor = GreenText,
            AutoSize = true,
            Location = new Point(40, 160)
        };

        _txtUsername.Font = new Font("Consolas", 14f);
        _txtUsername.Size = new Size(520, 34);
        _txtUsername.Location = new Point(40, 192);
        _txtUsername.BackColor = PanelBg;
        _txtUsername.ForeColor = GreenText;
        _txtUsername.BorderStyle = BorderStyle.FixedSingle;

        var lblPass = new Label
        {
            Text = "Şifre:",
            Font = new Font("Consolas", 12f),
            ForeColor = GreenText,
            AutoSize = true,
            Location = new Point(40, 248)
        };

        _txtPassword.Font = new Font("Consolas", 14f);
        _txtPassword.Size = new Size(520, 34);
        _txtPassword.Location = new Point(40, 280);
        _txtPassword.BackColor = PanelBg;
        _txtPassword.ForeColor = GreenText;
        _txtPassword.BorderStyle = BorderStyle.FixedSingle;
        _txtPassword.PasswordChar = '●';

        var lblPassConfirm = new Label
        {
            Text = "Şifre Tekrar:",
            Font = new Font("Consolas", 12f),
            ForeColor = GreenText,
            AutoSize = true,
            Location = new Point(40, 336)
        };

        _txtPasswordConfirm.Font = new Font("Consolas", 14f);
        _txtPasswordConfirm.Size = new Size(520, 34);
        _txtPasswordConfirm.Location = new Point(40, 368);
        _txtPasswordConfirm.BackColor = PanelBg;
        _txtPasswordConfirm.ForeColor = GreenText;
        _txtPasswordConfirm.BorderStyle = BorderStyle.FixedSingle;
        _txtPasswordConfirm.PasswordChar = '●';

        _lblError.Text = "";
        _lblError.Font = new Font("Consolas", 10f);
        _lblError.ForeColor = Color.OrangeRed;
        _lblError.AutoSize = true;
        _lblError.Location = new Point(40, 420);

        var separator2 = new Panel
        {
            BackColor = Color.FromArgb(0, 100, 0),
            Size = new Size(540, 1),
            Location = new Point(30, 470)
        };

        var btnBack = new Button
        {
            Text = "◀  Geri",
            Font = new Font("Consolas", 11f),
            Size = new Size(140, 42),
            Location = new Point(40, 484),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(40, 40, 40),
            ForeColor = Color.Gray,
            Cursor = Cursors.Hand
        };
        btnBack.FlatAppearance.BorderColor = Color.Gray;
        btnBack.Click += (_, _) => ShowStep(0);

        var btnFinish = new Button
        {
            Text = "✔  Tamamla",
            Font = new Font("Consolas", 13f, FontStyle.Bold),
            Size = new Size(200, 46),
            Location = new Point(370, 482),
            FlatStyle = FlatStyle.Flat,
            BackColor = Color.FromArgb(20, 60, 20),
            ForeColor = GreenText,
            Cursor = Cursors.Hand
        };
        btnFinish.FlatAppearance.BorderColor = GreenText;
        btnFinish.Click += (_, _) => FinishSetup();

        AcceptButton = btnFinish;

        _credentialsPanel.Controls.AddRange([
            lblHeader, lblSubHeader, separator,
            lblUser, _txtUsername,
            lblPass, _txtPassword,
            lblPassConfirm, _txtPasswordConfirm,
            _lblError, separator2, btnBack, btnFinish
        ]);
    }

    private void ShowStep(int step)
    {
        _welcomePanel.Visible = step == 0;
        _credentialsPanel.Visible = step == 1;

        if (step == 1)
            _txtUsername.Focus();
    }

    private void FinishSetup()
    {
        var username = _txtUsername.Text.Trim();
        var password = _txtPassword.Text;
        var confirm = _txtPasswordConfirm.Text;

        if (string.IsNullOrWhiteSpace(username))
        {
            ShowError("Kullanıcı adı boş olamaz.");
            _txtUsername.Focus();
            return;
        }

        if (username.Length < 3)
        {
            ShowError("Kullanıcı adı en az 3 karakter olmalıdır.");
            _txtUsername.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(password))
        {
            ShowError("Şifre boş olamaz.");
            _txtPassword.Focus();
            return;
        }

        if (password.Length < 4)
        {
            ShowError("Şifre en az 4 karakter olmalıdır.");
            _txtPassword.Focus();
            return;
        }

        if (password != confirm)
        {
            ShowError("Şifreler eşleşmiyor.");
            _txtPasswordConfirm.Focus();
            _txtPasswordConfirm.SelectAll();
            return;
        }

        try
        {
            PasswordStore.SaveLogin(username, password);
            SetupCompleted = true;
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            ShowError($"Kayıt hatası: {ex.Message}");
        }
    }

    private void ShowError(string message)
    {
        _lblError.Text = $"⚠ {message}";
        _lblError.ForeColor = Color.OrangeRed;
    }
}
