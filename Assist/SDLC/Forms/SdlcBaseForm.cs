namespace Assist.SDLC.Forms;

using Assist.Services;
using Assist.SDLC.Services;

/// <summary>
/// Common base for all SDLC MDI child forms.
/// Initialises <see cref="SdlcRuntime"/>, applies the dark theme,
/// and provides shared layout helpers.
/// </summary>
internal abstract class SdlcBaseForm : Form
{
    protected SdlcBaseForm()
    {
        SdlcRuntime.EnsureInitialized();

        Font = new Font("Consolas", 10);
        StartPosition = FormStartPosition.CenterParent;
        BackColor = UITheme.Palette.Back;
        ForeColor = UITheme.Palette.Text;
    }

    /// <summary>Creates a themed <see cref="Label"/>.</summary>
    protected static Label CreateLabel(string text, int x, int y, int width = 200) => new()
    {
        Text = text,
        Location = new Point(x, y),
        Size = new Size(width, 22),
        ForeColor = UITheme.Palette.Text,
        Font = new Font("Consolas", 10)
    };

    /// <summary>Creates a themed <see cref="Button"/>.</summary>
    protected static Button CreateButton(string text, int x, int y, int width = 120, int height = 32) => new()
    {
        Text = text,
        Location = new Point(x, y),
        Size = new Size(width, height),
        FlatStyle = FlatStyle.Flat,
        BackColor = UITheme.Palette.Surface,
        ForeColor = UITheme.Palette.Text,
        Font = new Font("Consolas", 10),
        Cursor = Cursors.Hand
    };

    /// <summary>Creates a themed <see cref="TextBox"/>.</summary>
    protected static TextBox CreateTextBox(int x, int y, int width = 400, bool multiline = false, int height = 26) => new()
    {
        Location = new Point(x, y),
        Size = new Size(width, multiline ? height : 26),
        Multiline = multiline,
        BackColor = UITheme.Palette.Surface,
        ForeColor = UITheme.Palette.Text,
        BorderStyle = BorderStyle.FixedSingle,
        Font = new Font("Consolas", 10)
    };

    /// <summary>Creates a themed read-only <see cref="RichTextBox"/> for logs/output.</summary>
    protected static RichTextBox CreateOutputBox(DockStyle dock = DockStyle.Fill) => new()
    {
        ReadOnly = true,
        BackColor = UITheme.Palette.Surface,
        ForeColor = UITheme.Palette.Text,
        BorderStyle = BorderStyle.None,
        Font = new Font("Consolas", 9.5f),
        Dock = dock,
        WordWrap = false
    };

    /// <summary>Creates a themed <see cref="DataGridView"/>.</summary>
    protected static DataGridView CreateGrid(DockStyle dock = DockStyle.Fill)
    {
        var dgv = new DataGridView
        {
            Dock = dock,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            Font = new Font("Consolas", 9.5f)
        };
        UITheme.Apply(dgv);
        return dgv;
    }

    /// <summary>Thread-safe append to a <see cref="RichTextBox"/>.</summary>
    protected void AppendOutput(RichTextBox box, string text)
    {
        if (box.InvokeRequired)
            box.BeginInvoke(() => AppendOutput(box, text));
        else
        {
            box.AppendText(text + Environment.NewLine);
            box.ScrollToCaret();
        }
    }
}
