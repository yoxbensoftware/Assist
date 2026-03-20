namespace Assist.Forms.SystemTools
{
    partial class SystemInfoForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TextBox txtInfo;

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
            this.txtInfo = new System.Windows.Forms.TextBox();
            this.SuspendLayout();
            // 
            // txtInfo
            // 
            this.txtInfo.Dock = System.Windows.Forms.DockStyle.Fill;
            this.txtInfo.Font = new System.Drawing.Font("Consolas", 9F);
            this.txtInfo.Location = new System.Drawing.Point(0, 0);
            this.txtInfo.Multiline = true;
            this.txtInfo.Name = "txtInfo";
            this.txtInfo.ReadOnly = true;
            this.txtInfo.ScrollBars = System.Windows.Forms.ScrollBars.Both;
            this.txtInfo.Size = new System.Drawing.Size(600, 500);
            this.txtInfo.TabIndex = 0;
            this.txtInfo.WordWrap = false;
            this.txtInfo.BackColor = System.Drawing.Color.Black;
            this.txtInfo.ForeColor = System.Drawing.Color.FromArgb(0, 255, 0);
            // 
            // SystemInfoForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(600, 500);
            this.Controls.Add(this.txtInfo);
            this.Name = "SystemInfoForm";
            this.Text = "ℹ️ Sistem Bilgisi";
            this.BackColor = System.Drawing.Color.Black;
            this.ForeColor = System.Drawing.Color.FromArgb(0, 255, 0);
            this.Font = new System.Drawing.Font("Consolas", 10);
            this.ShowIcon = false;
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
