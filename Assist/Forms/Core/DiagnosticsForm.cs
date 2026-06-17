namespace Assist.Forms.Core;

using System.Diagnostics;
using Assist.Services;

internal sealed class DiagnosticsForm : Form
{
    private readonly Form _mdiHost;
    private readonly Process _process = Process.GetCurrentProcess();
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 2000 };
    private readonly RichTextBox _output = new();

    public DiagnosticsForm(Form mdiHost)
    {
        _mdiHost = mdiHost;
        Text = "Assist Diagnostics";
        ClientSize = new Size(780, 560);
        MinimumSize = new Size(640, 420);
        StartPosition = FormStartPosition.CenterParent;

        BuildUi();
        UITheme.Apply(this);
        _timer.Tick += (_, _) => RefreshSnapshot();
        Load += (_, _) =>
        {
            RefreshSnapshot();
            _timer.Start();
        };
    }

    private void BuildUi()
    {
        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 44,
            FlowDirection = FlowDirection.LeftToRight,
            Padding = new Padding(8),
            WrapContents = false
        };

        var btnRefresh = CreateButton("Yenile");
        btnRefresh.Click += (_, _) => RefreshSnapshot();
        var btnGc = CreateButton("GC Collect");
        btnGc.Click += (_, _) =>
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            RefreshSnapshot();
        };
        var btnLowPower = CreateButton("Low Power Aç/Kapat");
        btnLowPower.Click += (_, _) =>
        {
            AppSettingsService.Update(s => s.LowPowerMode = !s.LowPowerMode);
            RefreshSnapshot();
        };

        top.Controls.Add(btnRefresh);
        top.Controls.Add(btnGc);
        top.Controls.Add(btnLowPower);

        _output.Dock = DockStyle.Fill;
        _output.ReadOnly = true;
        _output.BorderStyle = BorderStyle.FixedSingle;
        _output.Font = new Font("Consolas", 10);
        _output.WordWrap = false;

        Controls.Add(_output);
        Controls.Add(top);
    }

    private static Button CreateButton(string text)
    {
        var button = new Button
        {
            Text = text,
            Width = 150,
            Height = 28,
            Margin = new Padding(0, 0, 8, 0)
        };
        UITheme.Apply(button);
        return button;
    }

    private void RefreshSnapshot()
    {
        try
        {
            _process.Refresh();
            var gc = GC.GetGCMemoryInfo();
            var settings = AppSettingsService.Current;
            var clipboard = ClipboardHistoryService.Instance;
            var clipboardOptions = clipboard?.GetOptions();
            var openForms = Application.OpenForms.Cast<Form>().ToList();
            var mdiChildren = _mdiHost.MdiChildren;

            _output.Text = $"""
                ASSIST RUNTIME SNAPSHOT
                Timestamp             : {DateTime.Now:yyyy-MM-dd HH:mm:ss}

                Process
                Working set           : {FormatBytes(_process.WorkingSet64)}
                Private memory        : {FormatBytes(_process.PrivateMemorySize64)}
                GC total memory       : {FormatBytes(GC.GetTotalMemory(forceFullCollection: false))}
                GC heap size          : {FormatBytes(gc.HeapSizeBytes)}
                Fragmented bytes      : {FormatBytes(gc.FragmentedBytes)}
                Threads               : {_process.Threads.Count}
                Handles               : {_process.HandleCount}
                Gen0 / Gen1 / Gen2    : {GC.CollectionCount(0)} / {GC.CollectionCount(1)} / {GC.CollectionCount(2)}

                Open UI
                Forms                 : {openForms.Count}
                MDI children          : {mdiChildren.Length}
                MDI titles            : {string.Join(", ", mdiChildren.Select(f => f.Text).Where(t => !string.IsNullOrWhiteSpace(t)))}

                Clipboard
                Service running       : {clipboard is not null}
                Entries               : {clipboard?.GetAll().Count ?? 0}
                Capacity              : {clipboardOptions?.capacity ?? 0}
                Poll interval         : {clipboardOptions?.intervalMs ?? 0} ms
                Sensitive filter      : {clipboardOptions?.filterSensitive ?? false}
                Max entry length      : {ClipboardHistoryService.MaxEntryLength:N0} chars

                Settings
                Low Power Mode        : {settings.LowPowerMode}
                Dashboard enabled     : {settings.DashboardEnabled}
                Clipboard enabled     : {settings.ClipboardHistoryEnabled}
                Restore session       : {settings.RestoreLastSession}
                Quick launcher        : {settings.QuickLauncherEnabled}
                Fast dashboard tick   : {AppSettingsService.FastDashboardInterval.TotalSeconds:N0}s
                Medium dashboard tick : {AppSettingsService.MediumDashboardInterval.TotalSeconds:N0}s
                Slow dashboard tick   : {AppSettingsService.SlowDashboardInterval.TotalMinutes:N0}m
                """;
        }
        catch (Exception ex)
        {
            _output.Text = $"Diagnostics refresh failed: {ex.Message}";
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _timer.Stop();
        _timer.Dispose();
        _process.Dispose();
        base.OnFormClosed(e);
    }

    private static string FormatBytes(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        double value = bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return $"{value:N1} {units[unit]}";
    }
}
