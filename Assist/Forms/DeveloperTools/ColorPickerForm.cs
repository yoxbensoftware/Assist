namespace Assist.Forms.DeveloperTools;

using System.Runtime.InteropServices;

/// <summary>
/// Color picker tool to capture colors from screen with hex and RGB values.
/// </summary>
internal sealed class ColorPickerForm : Form
{

    private readonly Panel _colorPreview = null!;
    private readonly Label _lblHex = null!;
    private readonly Label _lblRgb = null!;
    private readonly Label _lblHsv = null!;
    private readonly TextBox _txtHex = null!;
    private readonly TextBox _txtRgb = null!;
    private readonly System.Windows.Forms.Timer _pickerTimer = null!;
    private bool _isPicking;

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern uint GetPixel(IntPtr hdc, int nXPos, int nYPos);

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X;
        public int Y;
    }

    public ColorPickerForm()
    {
        Text = "Color Picker";
        ClientSize = new Size(600, 500);
        MinimumSize = new Size(500, 400);
        StartPosition = FormStartPosition.CenterParent;
        AutoScroll = true;
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== COLOR PICKER ===",
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        _colorPreview = new Panel
        {
            Location = new Point(20, 60),
            Width = 560,
            Height = 150,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Color.Gray,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        var btnPick = new Button
        {
            Text = "Start Picking (Press SPACE to capture)",
            Location = new Point(20, 230),
            Width = 350,
            Height = 35,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnPick.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnPick.Click += (_, _) => TogglePicker();

        var lblInfo = new Label
        {
            Text = "Tıklayın ve fare ile ekranda gezinin. SPACE tuşu ile rengi yakalayın.",
            Location = new Point(20, 275),
            Width = 560,
            ForeColor = Color.Yellow,
            Font = new Font("Consolas", 8)
        };

        _lblHex = new Label
        {
            Text = "HEX:",
            Location = new Point(20, 310),
            Width = 100,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtHex = new TextBox
        {
            Location = new Point(130, 307),
            Width = 200,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true
        };

        var btnCopyHex = new Button
        {
            Text = "Copy",
            Location = new Point(340, 305),
            Width = 80,
            Height = 25,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat
        };
        btnCopyHex.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnCopyHex.Click += (_, _) => CopyToClipboard(_txtHex.Text);

        _lblRgb = new Label
        {
            Text = "RGB:",
            Location = new Point(20, 350),
            Width = 100,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        _txtRgb = new TextBox
        {
            Location = new Point(130, 347),
            Width = 200,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle,
            ReadOnly = true
        };

        var btnCopyRgb = new Button
        {
            Text = "Copy",
            Location = new Point(340, 345),
            Width = 80,
            Height = 25,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat
        };
        btnCopyRgb.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnCopyRgb.Click += (_, _) => CopyToClipboard(_txtRgb.Text);

        _lblHsv = new Label
        {
            Text = "HSV: -",
            Location = new Point(20, 390),
            Width = 560,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 9),
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
        };

        Controls.AddRange([lblTitle, _colorPreview, btnPick, lblInfo, _lblHex, _txtHex, btnCopyHex, _lblRgb, _txtRgb, btnCopyRgb, _lblHsv]);

        _pickerTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _pickerTimer.Tick += PickerTimer_Tick;

        KeyPreview = true;
        KeyDown += ColorPickerForm_KeyDown;
    }

    private void TogglePicker()
    {
        _isPicking = !_isPicking;
        if (_isPicking)
        {
            _pickerTimer.Start();
            Cursor = Cursors.Cross;
            TopMost = true; // Keep form on top while picking
            Text = "Color Picker - PICKING... (SPACE to capture, ESC to cancel)";
        }
        else
        {
            _pickerTimer.Stop();
            Cursor = Cursors.Default;
            TopMost = false;
            Text = "Color Picker";
        }
    }

    private void PickerTimer_Tick(object? sender, EventArgs e)
    {
        if (!_isPicking) return;

        try
        {
            GetCursorPos(out var point);
            var hdc = GetDC(IntPtr.Zero);

            if (hdc == IntPtr.Zero)
            {
                _colorPreview.BackColor = Color.Gray;
                return;
            }

            var pixel = GetPixel(hdc, point.X, point.Y);
            ReleaseDC(IntPtr.Zero, hdc);

            var r = (byte)(pixel & 0xFF);
            var g = (byte)((pixel >> 8) & 0xFF);
            var b = (byte)((pixel >> 16) & 0xFF);

            var color = Color.FromArgb(r, g, b);
            _colorPreview.BackColor = color;

            // Show live preview in textboxes
            _txtHex.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
            _txtRgb.Text = $"rgb({color.R}, {color.G}, {color.B})";

            var hsv = RgbToHsv(color);
            _lblHsv.Text = $"HSV: H={hsv.h:F0}° S={hsv.s:F2} V={hsv.v:F2}";
        }
        catch
        {
            _colorPreview.BackColor = Color.Gray;
        }
    }

    private void ColorPickerForm_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Space && _isPicking)
        {
            var currentColor = _colorPreview.BackColor;
            _txtHex.Text = $"#{currentColor.R:X2}{currentColor.G:X2}{currentColor.B:X2}";
            _txtRgb.Text = $"rgb({currentColor.R}, {currentColor.G}, {currentColor.B})";

            var hsv = RgbToHsv(currentColor);
            _lblHsv.Text = $"HSV: H={hsv.h:F0}° S={hsv.s:F2} V={hsv.v:F2}";

            _isPicking = false;
            _pickerTimer.Stop();
            Cursor = Cursors.Default;
            TopMost = false;
            Text = "Color Picker - Color Captured!";

            MessageBox.Show($"Renk yakalandı!\n\nHEX: {_txtHex.Text}\nRGB: {_txtRgb.Text}\n\nDeğerleri kopyalayabilirsiniz.",
                "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);

            Text = "Color Picker";
            e.Handled = true;
        }
        else if (e.KeyCode == Keys.Escape)
        {
            _isPicking = false;
            _pickerTimer.Stop();
            Cursor = Cursors.Default;
            TopMost = false;
            Text = "Color Picker";
            e.Handled = true;
        }
    }

    private static (double h, double s, double v) RgbToHsv(Color color)
    {
        double r = color.R / 255.0;
        double g = color.G / 255.0;
        double b = color.B / 255.0;

        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double delta = max - min;

        double h = 0;
        if (delta != 0)
        {
            if (max == r) h = 60 * (((g - b) / delta) % 6);
            else if (max == g) h = 60 * (((b - r) / delta) + 2);
            else h = 60 * (((r - g) / delta) + 4);
        }
        if (h < 0) h += 360;

        double s = max == 0 ? 0 : delta / max;
        double v = max;

        return (h, s, v);
    }

    private static void CopyToClipboard(string text)
    {
        if (!string.IsNullOrWhiteSpace(text))
        {
            Clipboard.SetText(text);
            MessageBox.Show("Panoya kopyalandı!", "Başarılı", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _pickerTimer?.Stop();
        _pickerTimer?.Dispose();
    }
}
