namespace Assist.Forms.Core
{
    partial class LoginForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TextBox txtUsername;
        private System.Windows.Forms.TextBox txtPassword;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.Label lblUsername;
        private System.Windows.Forms.Label lblPassword;
        private System.Windows.Forms.Panel pnlUsername;
        private System.Windows.Forms.Panel pnlPassword;

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
            txtUsername = new TextBox();
            txtPassword = new TextBox();
            btnLogin = new Button();
            lblUsername = new Label();
            lblPassword = new Label();
            pnlUsername = new Panel();
            pnlPassword = new Panel();
            var table = new TableLayoutPanel();
            SuspendLayout();
            AutoScaleDimensions = new SizeF(9F, 20F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(480, 200);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            Padding = new Padding(16);
            // 
            // table
            // 
            table.ColumnCount = 2;
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140F));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            table.Dock = DockStyle.Fill;
            table.RowCount = 3;
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 50F));
            table.RowStyles.Add(new RowStyle(SizeType.Absolute, 58F));
            table.Padding = new Padding(6, 10, 6, 6);
            // 
            // lblUsername
            // 
            lblUsername.AutoSize = false;
            lblUsername.Dock = DockStyle.Fill;
            lblUsername.Margin = new Padding(0, 0, 10, 0);
            lblUsername.Name = "lblUsername";
            lblUsername.Text = "Kullanıcı Adı";
            lblUsername.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pnlUsername — borderless Panel used to draw themed TextBox border
            // 
            pnlUsername.Dock = DockStyle.Fill;
            pnlUsername.Margin = new Padding(0, 10, 0, 10);
            pnlUsername.Name = "pnlUsername";
            pnlUsername.Padding = new Padding(1);
            pnlUsername.Paint += PnlTextBox_Paint;
            // 
            // txtUsername
            // 
            txtUsername.BorderStyle = BorderStyle.None;
            txtUsername.Dock = DockStyle.Fill;
            txtUsername.Name = "txtUsername";
            txtUsername.TabIndex = 1;
            // 
            // lblPassword
            // 
            lblPassword.AutoSize = false;
            lblPassword.Dock = DockStyle.Fill;
            lblPassword.Margin = new Padding(0, 0, 10, 0);
            lblPassword.Name = "lblPassword";
            lblPassword.Text = "Şifre";
            lblPassword.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // pnlPassword — borderless Panel used to draw themed TextBox border
            // 
            pnlPassword.Dock = DockStyle.Fill;
            pnlPassword.Margin = new Padding(0, 10, 0, 10);
            pnlPassword.Name = "pnlPassword";
            pnlPassword.Padding = new Padding(1);
            pnlPassword.Paint += PnlTextBox_Paint;
            // 
            // txtPassword
            // 
            txtPassword.BorderStyle = BorderStyle.None;
            txtPassword.Dock = DockStyle.Fill;
            txtPassword.Name = "txtPassword";
            txtPassword.PasswordChar = '*';
            txtPassword.TabIndex = 3;
            // 
            // btnLogin
            // 
            btnLogin.Dock = DockStyle.Fill;
            btnLogin.Margin = new Padding(0, 8, 0, 0);
            btnLogin.Name = "btnLogin";
            btnLogin.TabIndex = 4;
            btnLogin.Text = "Giriş Yap";
            btnLogin.FlatStyle = FlatStyle.Flat;
            btnLogin.FlatAppearance.BorderSize = 1;
            btnLogin.UseVisualStyleBackColor = false;
            btnLogin.Click += btnLogin_Click;
            // 
            // LoginForm
            // 
            pnlUsername.Controls.Add(txtUsername);
            pnlPassword.Controls.Add(txtPassword);
            Controls.Add(table);
            table.Controls.Add(lblUsername, 0, 0);
            table.Controls.Add(pnlUsername, 1, 0);
            table.Controls.Add(lblPassword, 0, 1);
            table.Controls.Add(pnlPassword, 1, 1);
            table.Controls.Add(btnLogin, 1, 2);
            Margin = new Padding(4, 5, 4, 5);
            Name = "LoginForm";
            ShowIcon = false;
            Text = "🔒 Assist";
            ResumeLayout(false);
            PerformLayout();
        }
    }
}
