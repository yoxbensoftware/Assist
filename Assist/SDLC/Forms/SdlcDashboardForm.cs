namespace Assist.SDLC.Forms;

using Assist.Services;
using Assist.SDLC.Domain;
using Assist.SDLC.Services;

/// <summary>Dashboard — overall system overview, active tasks, agent status.</summary>
internal sealed class SdlcDashboardForm : SdlcBaseForm
{
    private readonly DataGridView _agentGrid;
    private readonly Label _lblTasks;
    private readonly Label _lblNotifications;
    private readonly Label _lblApprovals;
    private readonly Button _btnRefresh;
    private readonly RichTextBox _eventLog;

    public SdlcDashboardForm()
    {
        Text = "🎯 SDLC Dashboard";
        Size = new Size(920, 620);

        // ── Top summary panel ─────────────────────────────
        var top = new Panel { Dock = DockStyle.Top, Height = 60 };
        _lblTasks = CreateLabel("Aktif: 0  |  Bekleyen: 0", 12, 8, 300);
        _lblNotifications = CreateLabel("Bildirimler: 0", 320, 8, 200);
        _lblApprovals = CreateLabel("Onay Bekleyen: 0", 530, 8, 200);
        _btnRefresh = CreateButton("Yenile", 770, 6, 100, 28);
        _btnRefresh.Click += (_, _) => RefreshData();

        top.Controls.AddRange([_lblTasks, _lblNotifications, _lblApprovals, _btnRefresh]);
        Controls.Add(top);

        // ── Agent grid (top half) ─────────────────────────
        var splitter = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            BackColor = UITheme.Palette.Back
        };

        _agentGrid = CreateGrid();
        _agentGrid.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { HeaderText = "Agent", Name = "Agent" },
            new DataGridViewTextBoxColumn { HeaderText = "Durum", Name = "State" },
            new DataGridViewTextBoxColumn { HeaderText = "Task", Name = "Task" },
        ]);
        splitter.Panel1.Controls.Add(_agentGrid);

        // ── Event log (bottom half) ───────────────────────
        _eventLog = CreateOutputBox();
        splitter.Panel2.Controls.Add(_eventLog);

        Controls.Add(splitter);

        // Subscribe to global events
        SdlcRuntime.EventBus.SubscribeAll(evt =>
        {
            if (IsDisposed) return;
            BeginInvoke(() => AppendOutput(_eventLog,
                $"[{evt.TimestampUtc:HH:mm:ss}] {evt.Type} — {evt.Summary}"));
        });

        Load += (_, _) =>
        {
            splitter.SplitterDistance = Math.Max(1, splitter.Height / 2);
            RefreshData();
        };
    }

    private void RefreshData()
    {
        var snapshots = SdlcRuntime.AgentCoordinator.GetAllSnapshots();
        _agentGrid.Rows.Clear();
        foreach (var s in snapshots)
            _agentGrid.Rows.Add(s.Role, s.State, s.CurrentTaskId ?? "—");

        var unread = SdlcRuntime.Notifications.GetUnread().Count;
        var pending = SdlcRuntime.Approvals.GetPending().Count;
        _lblNotifications.Text = $"Bildirimler: {unread}";
        _lblApprovals.Text = $"Onay Bekleyen: {pending}";
    }
}
