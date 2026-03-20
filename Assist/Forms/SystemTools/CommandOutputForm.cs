namespace Assist.Forms.SystemTools;

/// <summary>
/// Form to display command output text.
/// </summary>
internal sealed class CommandOutputForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);

    private readonly TextBox _txtOutput = null!;

    public CommandOutputForm()
    {
        Text = "Komut Çıktısı";
        ClientSize = new Size(700, 400);
        BackColor = Color.Black;
        ForeColor = GreenText;

        _txtOutput = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            Font = new Font("Consolas", 10),
            BackColor = Color.Black,
            ForeColor = GreenText
        };

        Controls.Add(_txtOutput);
    }

    public void SetOutput(string text) => _txtOutput.Text = text;
}
