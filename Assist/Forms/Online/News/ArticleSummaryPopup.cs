namespace Assist.Forms.Online.News;

/// <summary>
/// Small popup form that displays a brief summary of a news article.
/// </summary>
internal sealed class ArticleSummaryPopup : Form
{
    public ArticleSummaryPopup(string articleTitle, string summary)
    {
        Text = "📋 Makale Özeti";
        ClientSize = new Size(520, 400);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        KeyPreview = true;

        BackColor = Color.FromArgb(30, 30, 30);
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = articleTitle,
            Dock = DockStyle.Top,
            Height = 52,
            Padding = new Padding(8, 6, 8, 6),
            Font = new Font("Consolas", 10, FontStyle.Bold),
            ForeColor = AppConstants.AccentText,
            BackColor = Color.FromArgb(25, 25, 25),
            AutoEllipsis = true
        };

        var txtSummary = new TextBox
        {
            Text = summary,
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.FromArgb(20, 20, 20),
            ForeColor = Color.FromArgb(210, 210, 210),
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.None
        };

        var btnClose = new Button
        {
            Text = "Kapat",
            Dock = DockStyle.Bottom,
            Height = 35,
            BackColor = Color.FromArgb(30, 30, 30),
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat
        };
        btnClose.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnClose.Click += (_, _) => Close();

        Controls.Add(txtSummary);
        Controls.Add(lblTitle);
        Controls.Add(btnClose);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape) Close();
        };
    }
}
