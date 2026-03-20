namespace Assist.Forms.DeveloperTools;

/// <summary>
/// VS Compare-style text diff tool with colored side-by-side input and highlighted diff output.
/// </summary>
internal sealed class TextDiffForm : Form
{
    private static readonly Color GreenText     = Color.FromArgb(0, 255, 0);
    private static readonly Color ColorAdded    = Color.FromArgb(50,  255, 120);
    private static readonly Color ColorRemoved  = Color.FromArgb(255, 80,  80);
    private static readonly Color ColorChgOld   = Color.FromArgb(255, 220, 60);
    private static readonly Color ColorChgNew   = Color.FromArgb(255, 165, 50);
    private static readonly Color ColorMeta     = Color.FromArgb(80,  180, 255);
    private static readonly Color ColorSame     = Color.FromArgb(85,  85,  85);

    private readonly RichTextBox _rtbLeft;
    private readonly RichTextBox _rtbRight;
    private readonly RichTextBox _rtbDiff;
    private readonly Label       _lblStatus;

    public TextDiffForm()
    {
        Text          = "Text Diff Tool \u2014 Compare";
        WindowState   = FormWindowState.Maximized;
        MinimumSize   = new Size(1200, 750);
        BackColor     = Color.Black;
        ForeColor     = GreenText;
        Font          = new Font("Consolas", 10);

        // Toolbar
        var toolbar = BuildToolbar(
            out var btnCompare,
            out var btnClear,
            out var btnSwap,
            out var btnCopy,
            out _lblStatus);

        // Outer splitter: top = inputs, bottom = diff result
        var outerSplit = new SplitContainer
        {
            Dock        = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            BackColor   = Color.FromArgb(28, 28, 28),
            BorderStyle = BorderStyle.None,
            SplitterWidth = 5
        };

        // Inner splitter: left = original, right = modified
        var inputSplit = new SplitContainer
        {
            Dock        = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            BackColor   = Color.FromArgb(28, 28, 28),
            BorderStyle = BorderStyle.None,
            SplitterWidth = 5
        };

        _rtbLeft  = MakeInputRtb();
        _rtbRight = MakeInputRtb();
        inputSplit.Panel1.Controls.Add(WrapInHeader("\u25c4  ORIGINAL", _rtbLeft));
        inputSplit.Panel2.Controls.Add(WrapInHeader("\u25ba  MODIFIED", _rtbRight));

        // Diff output
        _rtbDiff = new RichTextBox
        {
            Dock        = DockStyle.Fill,
            ReadOnly    = true,
            BackColor   = Color.FromArgb(8, 8, 8),
            ForeColor   = GreenText,
            Font        = new Font("Consolas", 9),
            BorderStyle = BorderStyle.None,
            WordWrap    = false,
            ScrollBars  = RichTextBoxScrollBars.Both,
            DetectUrls  = false
        };

        outerSplit.Panel1.Controls.Add(inputSplit);
        outerSplit.Panel2.Controls.Add(
            WrapInHeader("\u2295  DIFF RESULTS     [ + added ]  [ \u2212 removed ]  [ ~ changed ]  [ \u00b7 same ]",
                         _rtbDiff));

        Controls.Add(outerSplit);
        Controls.Add(toolbar);

        Load += (_, _) =>
        {
            outerSplit.SplitterDistance = Math.Max(120, outerSplit.Height / 2);
            inputSplit.SplitterDistance = Math.Max(120, inputSplit.Width  / 2);
        };

        btnCompare.Click += (_, _) => RunCompare();
        btnClear.Click   += (_, _) => ClearAll();
        btnSwap.Click    += (_, _) => SwapInputs();
        btnCopy.Click    += (_, _) => CopyDiff();
    }

    // ── UI helpers ──────────────────────────────────────────────────────────

    private Panel BuildToolbar(
        out Button btnCompare, out Button btnClear,
        out Button btnSwap,   out Button btnCopy,
        out Label  lblStatus)
    {
        var bar = new Panel
        {
            Dock      = DockStyle.Top,
            Height    = 46,
            BackColor = Color.FromArgb(12, 12, 12)
        };
        bar.Controls.Add(new Panel
        {
            Dock      = DockStyle.Bottom,
            Height    = 1,
            BackColor = Color.FromArgb(0, 120, 0)
        });

        var x = 10;
        var title = new Label
        {
            Text      = "\u25c8 TEXT DIFF \u2014 COMPARE",
            Location  = new Point(x, 13),
            AutoSize  = true,
            ForeColor = GreenText,
            Font      = new Font("Consolas", 11, FontStyle.Bold),
            BackColor = Color.Transparent
        };
        x += 230;

        btnCompare = MakeToolBtn("\u25ba Compare", ref x);
        btnClear   = MakeToolBtn("\u2715 Clear",   ref x);
        btnSwap    = MakeToolBtn("\u21c4 Swap",    ref x);
        btnCopy    = MakeToolBtn("\u2398 Copy Diff", ref x);

        lblStatus = new Label
        {
            Text      = "Ready \u2014 paste text into both panels and click Compare",
            Location  = new Point(x + 10, 15),
            AutoSize  = true,
            ForeColor = Color.FromArgb(0, 180, 200),
            Font      = new Font("Consolas", 9),
            BackColor = Color.Transparent
        };

        bar.Controls.AddRange([title, btnCompare, btnClear, btnSwap, btnCopy, lblStatus]);
        return bar;
    }

    private static Button MakeToolBtn(string text, ref int x)
    {
        var btn = new Button
        {
            Text      = text,
            Location  = new Point(x, 9),
            Width     = 112,
            Height    = 28,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Consolas", 9, FontStyle.Bold)
        };
        btn.FlatAppearance.BorderColor = Color.FromArgb(0, 100, 0);
        x += 120;
        return btn;
    }

    private static Panel WrapInHeader(string title, Control content)
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Black };
        var header = new Label
        {
            Text      = title,
            Dock      = DockStyle.Top,
            Height    = 24,
            ForeColor = Color.FromArgb(80, 200, 220),
            Font      = new Font("Consolas", 9, FontStyle.Bold),
            BackColor = Color.FromArgb(14, 14, 14),
            TextAlign = ContentAlignment.MiddleLeft,
            Padding   = new Padding(6, 0, 0, 0)
        };
        // order: header (Top, first) → content (Fill, last)
        panel.Controls.Add(header);
        panel.Controls.Add(content);
        return panel;
    }

    private static RichTextBox MakeInputRtb() => new()
    {
        Dock        = DockStyle.Fill,
        BackColor   = Color.FromArgb(10, 10, 10),
        ForeColor   = Color.FromArgb(0, 230, 0),
        Font        = new Font("Consolas", 10),
        BorderStyle = BorderStyle.None,
        WordWrap    = false,
        ScrollBars  = RichTextBoxScrollBars.Both,
        AcceptsTab  = true,
        DetectUrls  = false
    };

    // ── Diff logic ──────────────────────────────────────────────────────────

    private void RunCompare()
    {
        try
        {
            var lines1 = SplitLines(_rtbLeft.Text);
            var lines2 = SplitLines(_rtbRight.Text);

            _rtbDiff.Clear();

            if (lines1.SequenceEqual(lines2))
            {
                Append("\u2713  Texts are identical \u2014 no differences found.\r\n", Color.FromArgb(0, 200, 100));
                SetStatus("\u2713 Identical", Color.FromArgb(0, 200, 100));
                return;
            }

            int maxLines = Math.Max(lines1.Length, lines2.Length);
            int added = 0, removed = 0, changed = 0;

            Append(
                $"\u2550\u2550\u2550 DIFF  {DateTime.Now:HH:mm:ss}  \u2550\u2550\u2550" +
                $"  Original: {lines1.Length} lines  \u2502  Modified: {lines2.Length} lines \u2550\u2550\u2550\r\n\r\n",
                ColorMeta);

            for (int i = 0; i < maxLines; i++)
            {
                var l1 = i < lines1.Length ? lines1[i] : null;
                var l2 = i < lines2.Length ? lines2[i] : null;
                var n  = $"{i + 1,5}";

                if (l1 is null)
                {
                    Append($"{n}  +  {l2}\r\n", ColorAdded);
                    added++;
                }
                else if (l2 is null)
                {
                    Append($"{n}  -  {l1}\r\n", ColorRemoved);
                    removed++;
                }
                else if (l1 != l2)
                {
                    Append($"{n}  ~ \u25c4  {l1}\r\n", ColorChgOld);
                    Append($"       \u25ba  {l2}\r\n",  ColorChgNew);
                    changed++;
                }
                else
                {
                    Append($"{n}  \u00b7  {l1}\r\n", ColorSame);
                }
            }

            Append("\r\n\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\u2500\r\n", ColorMeta);
            Append($"  + Added: {added}   \u2212 Removed: {removed}   ~ Changed: {changed}   Total diff: {added + removed + changed}\r\n", ColorMeta);

            SetStatus(
                $"+{added}  \u2212{removed}  ~{changed}  ({added + removed + changed} diffs)",
                Color.FromArgb(255, 200, 60));
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", Color.Red);
        }
    }

    private void Append(string text, Color color)
    {
        _rtbDiff.SelectionStart  = _rtbDiff.TextLength;
        _rtbDiff.SelectionLength = 0;
        _rtbDiff.SelectionColor  = color;
        _rtbDiff.AppendText(text);
    }

    private void SetStatus(string text, Color color)
    {
        _lblStatus.Text      = text;
        _lblStatus.ForeColor = color;
    }

    private static string[] SplitLines(string text) =>
        text.Split(["\r\n", "\r", "\n"], StringSplitOptions.None);

    private void ClearAll()
    {
        _rtbLeft.Clear();
        _rtbRight.Clear();
        _rtbDiff.Clear();
        SetStatus("Ready \u2014 paste text into both panels and click Compare",
                  Color.FromArgb(0, 180, 200));
    }

    private void SwapInputs()
    {
        (_rtbLeft.Text, _rtbRight.Text) = (_rtbRight.Text, _rtbLeft.Text);
        _rtbDiff.Clear();
        SetStatus("Swapped \u2014 click Compare to refresh diff", Color.FromArgb(0, 180, 200));
    }

    private void CopyDiff()
    {
        if (string.IsNullOrEmpty(_rtbDiff.Text)) return;
        Clipboard.SetText(_rtbDiff.Text);
        SetStatus("Diff copied to clipboard", Color.FromArgb(0, 180, 200));
    }
}
