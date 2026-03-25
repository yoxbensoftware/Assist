namespace Assist.Forms.Core;

using Assist.Services;

/// <summary>
/// Login form that authenticates the user against stored credentials.
/// </summary>
internal partial class LoginForm : Form
{
    public bool IsAuthenticated { get; private set; }

    public LoginForm()
    {
        InitializeComponent();
        AcceptButton = btnLogin;

        txtUsername.GotFocus += (s, e) => pnlUsername.Invalidate();
        txtUsername.LostFocus += (s, e) => pnlUsername.Invalidate();
        txtPassword.GotFocus += (s, e) => pnlPassword.Invalidate();
        txtPassword.LostFocus += (s, e) => pnlPassword.Invalidate();
    }

    /// <summary>
    /// Draws a themed border around the text box panel, highlighting when focused.
    /// </summary>
    private void PnlTextBox_Paint(object? sender, PaintEventArgs e)
    {
        if (sender is not Panel pnl) return;
        var p = UITheme.Palette;
        bool focused = pnl.Controls.Count > 0 && pnl.Controls[0].Focused;
        using var pen = new Pen(focused ? p.Accent : p.Surface2);
        e.Graphics.DrawRectangle(pen, 0, 0, pnl.Width - 1, pnl.Height - 1);
    }

    /// <summary>
    /// Validates the entered credentials against the stored login and authenticates the user.
    /// </summary>
    private void btnLogin_Click(object sender, EventArgs e)
    {
        var storedLogin = PasswordStore.LoadLogin();
        var enteredUsername = txtUsername.Text;
        var enteredPassword = txtPassword.Text;

        if (storedLogin is not null &&
            enteredUsername == storedLogin.Value.username &&
            enteredPassword == storedLogin.Value.password)
        {
            AuthenticateAndClose();
        }
        else
        {
            ShowAuthenticationError();
        }
    }

    /// <summary>
    /// Sets the authentication flag and closes the form with a success result.
    /// </summary>
    private void AuthenticateAndClose()
    {
        IsAuthenticated = true;
        DialogResult = DialogResult.OK;
        Close();
    }

    /// <summary>
    /// Displays an authentication error message and clears the password field.
    /// </summary>
    private void ShowAuthenticationError()
    {
        MessageBox.Show(
            "Kullanıcı adı veya şifre yanlış.",
            "Hata",
            MessageBoxButtons.OK,
            MessageBoxIcon.Error);
        txtPassword.Clear();
    }
}
