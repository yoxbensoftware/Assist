namespace Assist.Forms.Core;

using Assist.Services;

internal sealed class AppSettingsForm : Form
{
    private readonly CheckBox _chkLowPower = new();
    private readonly CheckBox _chkDashboard = new();
    private readonly CheckBox _chkClipboard = new();
    private readonly CheckBox _chkRestoreSession = new();
    private readonly CheckBox _chkQuickLauncher = new();
    private readonly NumericUpDown _numClipboardNormal = new();
    private readonly NumericUpDown _numClipboardLowPower = new();

    public AppSettingsForm()
    {
        Text = "Assist Ayarları";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(560, 420);
        MinimumSize = new Size(560, 420);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        BuildUi();
        LoadValues();
        UITheme.Apply(this);
    }

    private void BuildUi()
    {
        var p = UITheme.Palette;
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(16),
            ColumnCount = 2,
            RowCount = 9,
            BackColor = p.Back
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));

        var title = new Label
        {
            Text = "Assist genel davranışı",
            Dock = DockStyle.Fill,
            Font = new Font("Consolas", 12, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft
        };
        root.Controls.Add(title, 0, 0);
        root.SetColumnSpan(title, 2);

        AddCheck(root, _chkLowPower, "Low Power Mode", 1);
        AddCheck(root, _chkDashboard, "Dashboard paneli", 2);
        AddCheck(root, _chkClipboard, "Pano geçmişi", 3);
        AddCheck(root, _chkRestoreSession, "Son oturumu geri yükle", 4);
        AddCheck(root, _chkQuickLauncher, "Ctrl+K hızlı başlatıcı", 5);

        AddNumber(root, "Normal pano interval (ms)", _numClipboardNormal, 6, 500, 60_000);
        AddNumber(root, "Low power pano interval (ms)", _numClipboardLowPower, 7, 1000, 60_000);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 12, 0, 0)
        };

        var btnSave = CreateButton("Kaydet");
        btnSave.Click += (_, _) => SaveAndClose();
        var btnCancel = CreateButton("İptal");
        btnCancel.Click += (_, _) => Close();

        buttons.Controls.Add(btnSave);
        buttons.Controls.Add(btnCancel);
        root.Controls.Add(buttons, 0, 8);
        root.SetColumnSpan(buttons, 2);

        Controls.Add(root);
    }

    private static void AddCheck(TableLayoutPanel root, CheckBox checkBox, string text, int row)
    {
        checkBox.Text = text;
        checkBox.Dock = DockStyle.Fill;
        checkBox.AutoSize = false;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        root.Controls.Add(checkBox, 0, row);
        root.SetColumnSpan(checkBox, 2);
    }

    private static void AddNumber(TableLayoutPanel root, string label, NumericUpDown input, int row, int min, int max)
    {
        var lbl = new Label
        {
            Text = label,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };
        input.Dock = DockStyle.Fill;
        input.Minimum = min;
        input.Maximum = max;
        input.Increment = 500;
        input.ThousandsSeparator = true;
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
        root.Controls.Add(lbl, 0, row);
        root.Controls.Add(input, 1, row);
    }

    private static Button CreateButton(string text)
    {
        var btn = new Button
        {
            Text = text,
            Width = 110,
            Height = 34,
            Margin = new Padding(8, 0, 0, 0)
        };
        UITheme.Apply(btn);
        return btn;
    }

    private void LoadValues()
    {
        var settings = AppSettingsService.Current;
        _chkLowPower.Checked = settings.LowPowerMode;
        _chkDashboard.Checked = settings.DashboardEnabled;
        _chkClipboard.Checked = settings.ClipboardHistoryEnabled;
        _chkRestoreSession.Checked = settings.RestoreLastSession;
        _chkQuickLauncher.Checked = settings.QuickLauncherEnabled;
        _numClipboardNormal.Value = settings.NormalClipboardIntervalMs;
        _numClipboardLowPower.Value = settings.LowPowerClipboardIntervalMs;
    }

    private void SaveAndClose()
    {
        AppSettingsService.Update(settings =>
        {
            settings.LowPowerMode = _chkLowPower.Checked;
            settings.DashboardEnabled = _chkDashboard.Checked;
            settings.ClipboardHistoryEnabled = _chkClipboard.Checked;
            settings.RestoreLastSession = _chkRestoreSession.Checked;
            settings.QuickLauncherEnabled = _chkQuickLauncher.Checked;
            settings.NormalClipboardIntervalMs = (int)_numClipboardNormal.Value;
            settings.LowPowerClipboardIntervalMs = (int)_numClipboardLowPower.Value;
        });

        DialogResult = DialogResult.OK;
        Close();
    }
}
