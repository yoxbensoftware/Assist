namespace Assist.Forms.Passwords
{
    partial class PasswordListForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            var dgv = new System.Windows.Forms.DataGridView();
            // Apply grid styling via theme helper
            Assist.Services.UITheme.Apply(dgv);
            dgv.Dock = System.Windows.Forms.DockStyle.Fill;
            dgv.AllowUserToAddRows = false;
            dgv.AllowUserToDeleteRows = false;
            dgv.ReadOnly = true;
            dgv.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            dgv.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            dgv.RowTemplate.Height = 32;
            dgv.Columns.Add("Title", "Başlık");
            dgv.Columns.Add("Username", "Kullanıcı Adı");
            var passwordCol = new System.Windows.Forms.DataGridViewTextBoxColumn();
            passwordCol.Name = "Password";
            passwordCol.HeaderText = "Şifre";
            dgv.Columns.Add(passwordCol);
            dgv.Columns.Add("Notes", "Notlar");
            var eyeCol = new System.Windows.Forms.DataGridViewButtonColumn();
            eyeCol.Name = "Eye";
            eyeCol.HeaderText = "Göz";
            eyeCol.Text = "👁";
            eyeCol.UseColumnTextForButtonValue = true;
            dgv.Columns.Add(eyeCol);
            dgv.CellClick += (s, e) =>
            {
                if (e.RowIndex >= 0 && e.ColumnIndex == dgv.Columns["Eye"].Index)
                {
                    var cell = dgv.Rows[e.RowIndex].Cells["Password"];
                    if (cell.Tag == null || (bool)cell.Tag == true)
                    {
                        cell.Value = cell.ToolTipText; // gerçek şifreyi göster
                        cell.Tag = false;
                    }
                    else
                    {
                        cell.Value = new string('*', cell.ToolTipText.Length); // maskla
                        cell.Tag = true;
                    }
                }
                else if (e.RowIndex >= 0 && e.ColumnIndex != dgv.Columns["Eye"].Index)
                {
                    var cell = dgv.Rows[e.RowIndex].Cells["Password"];
                    var realPassword = cell.ToolTipText;
                    if (!string.IsNullOrEmpty(realPassword))
                    {
                        try
                        {
                            System.Windows.Forms.Clipboard.SetText(realPassword);
                            // notify clipboard service that app set the clipboard so it won't record duplicate
                            Assist.Services.ClipboardHistoryService.Instance?.NotifyClipboardSetByApp(realPassword);
                            System.Windows.Forms.MessageBox.Show("Şifre panoya kopyalandı.", "Bilgi", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Information);
                        }
                        catch { }
                    }
                }
            };
            Controls.Add(dgv);
            Name = "PasswordListForm";
            Text = "Şifreleri Gör";
            Load += (s, e) =>
            {
                dgv.Rows.Clear();
                Assist.Services.PasswordStore.LoadFromFile();
                foreach (var entry in Assist.Services.PasswordStore.Entries)
                {
                    var mask = new string('*', entry.GetDecryptedPassword().Length);
                    var idx = dgv.Rows.Add(entry.Title, entry.Username, mask, entry.Notes, "👁");
                    dgv.Rows[idx].Cells["Password"].ToolTipText = entry.GetDecryptedPassword();
                    dgv.Rows[idx].Cells["Password"].Tag = true; // masklı
                }
            };
            ResumeLayout(false);
        }
    }
}
