namespace Assist.Forms.Core;

using Assist.Services;

/// <summary>
/// Borderless splash screen shown while the main MDI form is loading.
/// </summary>
internal sealed class SplashForm : Form
{
    private static readonly string[] SpinnerFrames = ["|", "/", "—", "\\"];

    private readonly Label _spinnerLabel;
    private readonly Label _messageLabel;
    private readonly Label _titleLabel;
    private readonly System.Windows.Forms.Timer _timer;
    private int _frameIndex;

    /// <summary>
    /// Initializes the splash form with themed styling and an ASCII spinner animation.
    /// </summary>
    public SplashForm()
    {
        var p = UITheme.Palette;

        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        Size = new Size(340, 140);
        BackColor = p.Back;
        ShowInTaskbar = false;
        TopMost = true;
        DoubleBuffered = true;

        var border = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = p.Back,
            Padding = new Padding(1)
        };
        border.Paint += (_, e) =>
        {
            using var pen = new Pen(p.Accent);
            e.Graphics.DrawRectangle(pen, 0, 0, border.Width - 1, border.Height - 1);
        };

        _titleLabel = new Label
        {
            Text = "Assist",
            Font = new Font("Consolas", 20f, FontStyle.Bold),
            ForeColor = p.Accent,
            AutoSize = true,
            BackColor = p.Back
        };

        _spinnerLabel = new Label
        {
            Text = SpinnerFrames[0],
            Font = new Font("Consolas", 14f, FontStyle.Bold),
            ForeColor = p.Accent,
            AutoSize = true,
            BackColor = p.Back
        };

        _messageLabel = new Label
        {
            Text = "Yükleniyor...",
            Font = new Font("Consolas", 10f),
            ForeColor = p.Text,
            AutoSize = true,
            BackColor = p.Back
        };

        border.Controls.AddRange([_titleLabel, _spinnerLabel, _messageLabel]);
        border.Resize += (_, _) => CenterControls();
        Controls.Add(border);

        _timer = new System.Windows.Forms.Timer { Interval = 100 };
        _timer.Tick += OnTick;
        _timer.Start();
    }

    /// <summary>
    /// Advances the spinner to the next animation frame.
    /// </summary>
    private void OnTick(object? sender, EventArgs e)
    {
        _frameIndex = (_frameIndex + 1) % SpinnerFrames.Length;
        _spinnerLabel.Text = SpinnerFrames[_frameIndex];
    }

    /// <summary>
    /// Centers the title and loading indicator within the splash form.
    /// </summary>
    private void CenterControls()
    {
        var w = ClientSize.Width;
        _titleLabel.Location = new Point((w - _titleLabel.Width) / 2, 24);

        var spinnerTotalWidth = _spinnerLabel.Width + 8 + _messageLabel.Width;
        var spinnerX = (w - spinnerTotalWidth) / 2;
        var row2Y = _titleLabel.Bottom + 24;
        _spinnerLabel.Location = new Point(spinnerX, row2Y);
        _messageLabel.Location = new Point(spinnerX + _spinnerLabel.Width + 8, row2Y + 4);
    }

    /// <summary>
    /// Cleans up the animation timer on form close.
    /// </summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        base.OnFormClosed(e);
    }
}
