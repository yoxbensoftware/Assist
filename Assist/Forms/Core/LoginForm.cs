using System;
using System.Windows.Forms;

namespace Assist;

public partial class LoginForm : Form
{
    public bool IsAuthenticated { get; private set; }

    public LoginForm()
    {
        InitializeComponent();
        AcceptButton = btnLogin;
    }

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

    private void AuthenticateAndClose()
    {
        IsAuthenticated = true;
        DialogResult = DialogResult.OK;
        Close();
    }

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
