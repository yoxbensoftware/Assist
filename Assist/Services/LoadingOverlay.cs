namespace Assist.Services;

/// <summary>
/// Old school ASCII spinner loading overlay.
/// </summary>
internal sealed class LoadingOverlay : IDisposable
{
    private static readonly string[] SpinnerFrames = ["|", "/", "—", "\\"];

    private readonly Form _parentForm;
    private readonly Panel _overlay;
    private readonly Panel _box;
    private readonly Label _spinnerLabel;
    private readonly Label _messageLabel;
    private readonly System.Windows.Forms.Timer _timer;
    private int _frameIndex;
    private bool _disposed;

    public LoadingOverlay(Form parentForm, string message = "Yükleniyor...")
    {
        _parentForm = parentForm ?? throw new ArgumentNullException(nameof(parentForm));
        _box = new Panel
        {
            BorderStyle = BorderStyle.None
        };

        // Full-screen overlay with centered loading box
        _overlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = UITheme.Palette.Back,
            Visible = false
        };

        _box.Paint += (_, e) =>
        {
            using var pen = new Pen(UITheme.Palette.Accent);
            e.Graphics.DrawRectangle(pen, 0, 0, _box.Width - 1, _box.Height - 1);
        };

        _spinnerLabel = new Label
        {
            Text = SpinnerFrames[0],
            Font = new Font("Consolas", 20, FontStyle.Bold),
            ForeColor = UITheme.Palette.Accent,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        _messageLabel = new Label
        {
            Text = message,
            Font = new Font("Consolas", 11),
            ForeColor = UITheme.Palette.Text,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        _box.Controls.Add(_spinnerLabel);
        _box.Controls.Add(_messageLabel);
        _overlay.Controls.Add(_box);
        _overlay.Resize += (_, _) => CenterBox();
        UpdateBoxSize();

        _timer = new System.Windows.Forms.Timer { Interval = 100 };
        _timer.Tick += OnTimerTick;

        ThemeService.ThemeChanged += OnThemeChanged;
        _parentForm.Controls.Add(_overlay);
        _overlay.BringToFront();
        ApplyTheme();
    }

    public void Show(string? message = null)
    {
        if (_disposed) return;

        if (message is not null)
        {
            _messageLabel.Text = message;
        }

        // immediate show without fade
        // recalc size for the current message
        UpdateBoxSize();
        _overlay.Visible = true;
        _overlay.BringToFront();
        _timer.Start();
        _parentForm.Cursor = Cursors.WaitCursor;
        Application.DoEvents();
    }

    public void Hide()
    {
        if (_disposed) return;

        _timer.Stop();
        _overlay.Visible = false;
        _parentForm.Cursor = Cursors.Default;
    }

    public void UpdateMessage(string message)
    {
        if (_disposed) return;
        _messageLabel.Text = message;
        UpdateBoxSize();
        CenterBox();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _frameIndex = (_frameIndex + 1) % SpinnerFrames.Length;
        _spinnerLabel.Text = SpinnerFrames[_frameIndex];
    }

    private void CenterBox()
    {
        _box.Location = new Point((_overlay.Width - _box.Width) / 2, (_overlay.Height - _box.Height) / 2);
        _spinnerLabel.Location = new Point(12, (_box.Height - _spinnerLabel.Height) / 2);
        _messageLabel.Location = new Point(12 + _spinnerLabel.Width + 12, (_box.Height - _messageLabel.Height) / 2);
    }

    private void UpdateBoxSize()
    {
        // measure text sizes
        var spinnerSize = TextRenderer.MeasureText(SpinnerFrames[_frameIndex], _spinnerLabel.Font);
        var messageSize = TextRenderer.MeasureText(_messageLabel.Text ?? string.Empty, _messageLabel.Font);

        var width = spinnerSize.Width + 12 + messageSize.Width + 24; // paddings
        var height = Math.Max(spinnerSize.Height, messageSize.Height) + 20;

        _box.Size = new Size(Math.Max(140, width), Math.Max(40, height));
        _spinnerLabel.AutoSize = true;
        _messageLabel.AutoSize = true;
        CenterBox();
    }

    private void ApplyTheme()
    {
        var p = UITheme.Palette;
        _overlay.BackColor = p.Back;
        _box.BackColor = p.Surface;
        _spinnerLabel.ForeColor = p.Accent;
        _messageLabel.ForeColor = p.Text;
        _box.Invalidate();
    }

    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (_disposed) return;

        if (_overlay.InvokeRequired)
            _overlay.BeginInvoke(new Action(ApplyTheme));
        else
            ApplyTheme();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ThemeService.ThemeChanged -= OnThemeChanged;
        _timer.Stop();
        _timer.Dispose();
        _overlay.Dispose();
    }
}

/// <summary>
/// Static helper for showing loading state on any form.
/// </summary>
internal static class Loading
{
    public static async Task<T> RunAsync<T>(Form form, Func<Task<T>> action, string message = "Yükleniyor...")
    {
        using var overlay = new LoadingOverlay(form, message);
        overlay.Show();
        try
        {
            return await action();
        }
        finally
        {
            overlay.Hide();
        }
    }

    public static async Task RunAsync(Form form, Func<Task> action, string message = "Yükleniyor...")
    {
        using var overlay = new LoadingOverlay(form, message);
        overlay.Show();
        try
        {
            await action();
        }
        finally
        {
            overlay.Hide();
        }
    }
}
