namespace Assist.Services;

/// <summary>
/// Old school ASCII spinner loading overlay.
/// </summary>
public sealed class LoadingOverlay : IDisposable
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly string[] SpinnerFrames = ["|", "/", "—", "\\"];

    private readonly Form _parentForm;
    private readonly Panel _overlay;
    private readonly Label _spinnerLabel;
    private readonly Label _messageLabel;
    private readonly System.Windows.Forms.Timer _timer;
    private int _frameIndex;
    private bool _disposed;

    public LoadingOverlay(Form parentForm, string message = "Yükleniyor...")
    {
        _parentForm = parentForm ?? throw new ArgumentNullException(nameof(parentForm));

        // Full black overlay with centered loading box
        _overlay = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.Black,
            Visible = false
        };

        // Center box - size will be calculated based on content
        var box = new Panel
        {
            BackColor = Color.Black,
            BorderStyle = BorderStyle.FixedSingle
        };

        _spinnerLabel = new Label
        {
            Text = SpinnerFrames[0],
            Font = new Font("Consolas", 20, FontStyle.Bold),
            ForeColor = GreenText,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        _messageLabel = new Label
        {
            Text = message,
            Font = new Font("Consolas", 11),
            ForeColor = GreenText,
            AutoSize = true,
            BackColor = Color.Transparent
        };

        box.Controls.Add(_spinnerLabel);
        box.Controls.Add(_messageLabel);
        _overlay.Controls.Add(box);
        _overlay.Resize += (s, e) => CenterBox(box);
        // compute initial size based on text
        UpdateBoxSize(box);

        _timer = new System.Windows.Forms.Timer { Interval = 100 };
        _timer.Tick += OnTimerTick;

        _parentForm.Controls.Add(_overlay);
        _overlay.BringToFront();
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
        if (_overlay.Controls.Count > 0 && _overlay.Controls[0] is Panel p) UpdateBoxSize(p);
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
        if (_overlay.Controls.Count > 0 && _overlay.Controls[0] is Panel p) UpdateBoxSize(p);
        CenterLabels();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        _frameIndex = (_frameIndex + 1) % SpinnerFrames.Length;
        _spinnerLabel.Text = SpinnerFrames[_frameIndex];
    }
    private void CenterLabels()
    {
        // kept for backward compatibility - center labels within overlay
        if (_overlay.Controls.Count == 0) return;
        var box = _overlay.Controls[0] as Panel;
        if (box is null) return;
        CenterBox(box);
    }

    private void CenterBox(Panel box)
    {
        if (box is null) return;
        box.Location = new Point((_overlay.Width - box.Width) / 2, (_overlay.Height - box.Height) / 2);

        // position spinner and message inside box
        _spinnerLabel.Location = new Point(12, (box.Height - _spinnerLabel.Height) / 2);
        _messageLabel.Location = new Point(12 + _spinnerLabel.Width + 12, (box.Height - _messageLabel.Height) / 2);
    }

    private void UpdateBoxSize(Panel box)
    {
        if (box is null) return;
        // measure text sizes
        var spinnerSize = TextRenderer.MeasureText(SpinnerFrames[_frameIndex], _spinnerLabel.Font);
        var messageSize = TextRenderer.MeasureText(_messageLabel.Text ?? string.Empty, _messageLabel.Font);

        var width = spinnerSize.Width + 12 + messageSize.Width + 24; // paddings
        var height = Math.Max(spinnerSize.Height, messageSize.Height) + 20;

        box.Size = new Size(Math.Max(140, width), Math.Max(40, height));
        // reposition labels inside box after resizing
        _spinnerLabel.AutoSize = true;
        _messageLabel.AutoSize = true;
        CenterBox(box);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _timer.Stop();
        _timer.Dispose();
        _overlay.Dispose();
    }
}

/// <summary>
/// Static helper for showing loading state on any form.
/// </summary>
public static class Loading
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
