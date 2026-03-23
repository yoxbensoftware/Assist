using Assist.Services;

namespace Assist.Forms.Passwords;

internal partial class PasswordEditForm : Form
{
    private readonly string _originalTitle;

    public PasswordEditForm(string? title, string? username, string? password, string? notes)
    {
        InitializeComponent();
        _originalTitle = title ?? string.Empty;

        txtTitle.Text = title;
        txtUsername.Text = username;
        txtPassword.Text = password;
        txtNotes.Text = notes;
    }

    private void btnUpdate_Click(object sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(txtTitle.Text))
        {
            MessageBox.Show("Başlık boş olamaz.", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var entry = PasswordStore.Entries.FirstOrDefault(x => x.Title == _originalTitle);
        if (entry is null)
        {
            MessageBox.Show("Kayıt bulunamadı.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        entry.Title = txtTitle.Text.Trim();
        entry.Username = txtUsername.Text.Trim();
        entry.SetPassword(txtPassword.Text);
        entry.Notes = txtNotes.Text.Trim();

        PasswordStore.SaveToFile();
        MessageBox.Show("Kayıt güncellendi.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        Close();
    }
}
