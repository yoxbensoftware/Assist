using Assist.Models;
using Assist.Services;

namespace Assist.Forms.Passwords;

internal partial class PasswordEntryForm : Form
{
    public PasswordEntryForm()
    {
        InitializeComponent();
    }

    private void btnSave_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtTitle.Text))
        {
            MessageBox.Show("Başlık boş olamaz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var entry = new PasswordEntry
        {
            Title = txtTitle.Text.Trim(),
            Username = txtUsername.Text.Trim(),
            Notes = txtNotes.Text.Trim()
        };
        entry.SetPassword(txtPassword.Text);

        PasswordStore.Add(entry);
        MessageBox.Show("Şifre kaydedildi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }
}
