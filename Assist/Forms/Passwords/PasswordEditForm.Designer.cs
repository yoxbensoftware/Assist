namespace Assist
{
    partial class PasswordEditForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TextBox txtTitle;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.TextBox txtNotes;
        private System.Windows.Forms.Button btnUpdate;
        private System.Windows.Forms.Label lblTitle;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.Label lblNotes;
        private System.Windows.Forms.CheckBox chkShowPassword;

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
            this.txtTitle = new System.Windows.Forms.TextBox();
            this.txtUsername = new System.Windows.Forms.TextBox();
            this.txtPassword = new System.Windows.Forms.TextBox();
            this.txtNotes = new System.Windows.Forms.TextBox();
            this.btnUpdate = new System.Windows.Forms.Button();
            this.lblTitle = new System.Windows.Forms.Label();
            this.lblUsername = new System.Windows.Forms.Label();
            this.lblPassword = new System.Windows.Forms.Label();
            this.lblNotes = new System.Windows.Forms.Label();
            this.chkShowPassword = new System.Windows.Forms.CheckBox();
            this.SuspendLayout();
            // 
            // lblTitle
            // 
            this.lblTitle.AutoSize = true;
            this.lblTitle.Location = new System.Drawing.Point(12, 15);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(32, 15);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Başlık";
            // 
            // txtTitle
            // 
            this.txtTitle.Location = new System.Drawing.Point(100, 12);
            this.txtTitle.Name = "txtTitle";
            this.txtTitle.Size = new System.Drawing.Size(200, 23);
            this.txtTitle.TabIndex = 1;
            // 
            // lblUsername
            // 
            this.lblUsername.AutoSize = true;
            this.lblUsername.Location = new System.Drawing.Point(12, 44);
            this.lblUsername.Name = "lblUsername";
            this.lblUsername.Size = new System.Drawing.Size(73, 15);
            this.lblUsername.TabIndex = 2;
            this.lblUsername.Text = "Kullanıcı Adı";
            // 
            // txtUsername
            // 
            this.txtUsername.Location = new System.Drawing.Point(100, 41);
            this.txtUsername.Name = "txtUsername";
            this.txtUsername.Size = new System.Drawing.Size(200, 23);
            this.txtUsername.TabIndex = 3;
            // 
            // lblPassword
            // 
            this.lblPassword.AutoSize = true;
            this.lblPassword.Location = new System.Drawing.Point(12, 73);
            this.lblPassword.Name = "lblPassword";
            this.lblPassword.Size = new System.Drawing.Size(39, 15);
            this.lblPassword.TabIndex = 4;
            this.lblPassword.Text = "Şifre";
            // 
            // txtPassword
            // 
            this.txtPassword.Location = new System.Drawing.Point(100, 70);
            this.txtPassword.Name = "txtPassword";
            this.txtPassword.Size = new System.Drawing.Size(200, 23);
            this.txtPassword.TabIndex = 5;
            this.txtPassword.PasswordChar = '*';
            // 
            // chkShowPassword
            // 
            this.chkShowPassword.AutoSize = true;
            this.chkShowPassword.Location = new System.Drawing.Point(100, 99);
            this.chkShowPassword.Name = "chkShowPassword";
            this.chkShowPassword.Size = new System.Drawing.Size(110, 19);
            this.chkShowPassword.TabIndex = 6;
            this.chkShowPassword.Text = "Şifreyi Göster";
            this.chkShowPassword.UseVisualStyleBackColor = true;
            this.chkShowPassword.CheckedChanged += (s, e) =>
            {
                txtPassword.PasswordChar = chkShowPassword.Checked ? '\0' : '*';
            };
            // 
            // lblNotes
            // 
            this.lblNotes.AutoSize = true;
            this.lblNotes.Location = new System.Drawing.Point(12, 128);
            this.lblNotes.Name = "lblNotes";
            this.lblNotes.Size = new System.Drawing.Size(36, 15);
            this.lblNotes.TabIndex = 7;
            this.lblNotes.Text = "Notlar";
            // 
            // txtNotes
            // 
            this.txtNotes.Location = new System.Drawing.Point(100, 125);
            this.txtNotes.Name = "txtNotes";
            this.txtNotes.Size = new System.Drawing.Size(200, 60);
            this.txtNotes.TabIndex = 8;
            this.txtNotes.Multiline = true;
            // 
            // btnUpdate
            // 
            this.btnUpdate.Location = new System.Drawing.Point(100, 195);
            this.btnUpdate.Name = "btnUpdate";
            this.btnUpdate.Size = new System.Drawing.Size(200, 23);
            this.btnUpdate.TabIndex = 9;
            this.btnUpdate.Text = "Güncelle";
            this.btnUpdate.UseVisualStyleBackColor = true;
            this.btnUpdate.Click += new System.EventHandler(this.btnUpdate_Click);
            // 
            // PasswordEditForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(320, 235);
            // Dark theme
            this.BackColor = System.Drawing.Color.FromArgb(18, 18, 18);
            this.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.Controls.Add(this.btnUpdate);
            this.Controls.Add(this.chkShowPassword);
            this.Controls.Add(this.txtNotes);
            this.Controls.Add(this.lblNotes);
            this.Controls.Add(this.txtPassword);
            this.Controls.Add(this.lblPassword);
            this.Controls.Add(this.txtUsername);
            this.Controls.Add(this.lblUsername);
            this.Controls.Add(this.txtTitle);
            this.Controls.Add(this.lblTitle);
            this.Name = "PasswordEditForm";
            this.Text = "✏️ Şifre Düzenle";
            this.Font = new System.Drawing.Font("Segoe UI", 10);
            this.ShowIcon = false;
            // Controls coloring
            txtTitle.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            txtTitle.ForeColor = System.Drawing.Color.WhiteSmoke;
            txtUsername.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            txtUsername.ForeColor = System.Drawing.Color.WhiteSmoke;
            txtPassword.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            txtPassword.ForeColor = System.Drawing.Color.WhiteSmoke;
            txtNotes.BackColor = System.Drawing.Color.FromArgb(30, 30, 30);
            txtNotes.ForeColor = System.Drawing.Color.WhiteSmoke;
            btnUpdate.BackColor = System.Drawing.Color.FromArgb(45, 45, 48);
            btnUpdate.ForeColor = System.Drawing.Color.WhiteSmoke;
            chkShowPassword.ForeColor = System.Drawing.Color.WhiteSmoke;
            lblTitle.ForeColor = System.Drawing.Color.WhiteSmoke;
            lblUsername.ForeColor = System.Drawing.Color.WhiteSmoke;
            lblPassword.ForeColor = System.Drawing.Color.WhiteSmoke;
            lblNotes.ForeColor = System.Drawing.Color.WhiteSmoke;
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
