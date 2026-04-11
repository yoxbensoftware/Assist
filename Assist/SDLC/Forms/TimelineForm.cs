namespace Assist.SDLC.Forms;

using Assist.SDLC.Messaging;
using Assist.SDLC.Services;

/// <summary>Timeline / Iteration Monitor — full event history timeline.</summary>
internal sealed class TimelineForm : SdlcBaseForm
{
    private readonly DataGridView _grid;

    public TimelineForm()
    {
        Text = "📈 Timeline / Iteration Monitor";
        Size = new Size(860, 480);

        var top = new Panel { Dock = DockStyle.Top, Height = 42 };
        var btn = CreateButton("Yenile", 12, 6, 100, 28);
        btn.Click += (_, _) => RefreshGrid();
        top.Controls.Add(btn);
        Controls.Add(top);

        _grid = CreateGrid();
        _grid.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { HeaderText = "Zaman", Name = "Time", Width = 140 },
            new DataGridViewTextBoxColumn { HeaderText = "Olay", Name = "Event", Width = 180 },
            new DataGridViewTextBoxColumn { HeaderText = "Agent", Name = "Agent", Width = 110 },
            new DataGridViewTextBoxColumn { HeaderText = "Task", Name = "Task", Width = 110 },
            new DataGridViewTextBoxColumn { HeaderText = "Özet", Name = "Summary" },
        ]);
        Controls.Add(_grid);

        // Auto-refresh on new events
        SdlcRuntime.EventBus.SubscribeAll(_ =>
        {
            if (IsDisposed) return;
            BeginInvoke(RefreshGrid);
        });

        Load += (_, _) => RefreshGrid();
    }

    private void RefreshGrid()
    {
        var history = SdlcRuntime.EventBus.GetHistory();
        _grid.Rows.Clear();
        foreach (var evt in history.OrderByDescending(e => e.TimestampUtc))
            _grid.Rows.Add(
                evt.TimestampUtc.ToLocalTime().ToString("HH:mm:ss.fff"),
                evt.Type,
                evt.SourceAgent?.ToString() ?? "—",
                evt.TaskId ?? "—",
                evt.Summary ?? "");
    }
}
