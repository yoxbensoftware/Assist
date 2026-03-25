namespace Assist.Forms.SystemTools;

/// <summary>
/// Form to display command output text.
/// </summary>
internal sealed class CommandOutputForm : Form
{

    private readonly TextBox _txtOutput = null!;

    public CommandOutputForm()
    {
        Text = "Komut Çıktısı";
        ClientSize = new Size(700, 400);
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;

        _txtOutput = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10),
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText
        };

        Controls.Add(_txtOutput);
    }

    public void SetOutput(string text) => _txtOutput.Text = text;
}
