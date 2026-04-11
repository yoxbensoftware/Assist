namespace Assist.SDLC.Forms;

using Assist.Services;
using Assist.SDLC.Domain;
using Assist.SDLC.Services;

/// <summary>Agent Console Hub — overview cards for all agents.</summary>
internal sealed class AgentConsoleHubForm : SdlcBaseForm
{
    private readonly DataGridView _grid;

    public AgentConsoleHubForm()
    {
        Text = "🤖 Agent Console Hub";
        Size = new Size(750, 400);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 42 };
        var btn = CreateButton("Yenile", 12, 6, 100, 28);
        btn.Click += (_, _) => RefreshGrid();
        topPanel.Controls.Add(btn);
        Controls.Add(topPanel);

        _grid = CreateGrid();
        _grid.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { HeaderText = "Agent", Name = "Agent" },
            new DataGridViewTextBoxColumn { HeaderText = "Durum", Name = "State" },
            new DataGridViewTextBoxColumn { HeaderText = "Task", Name = "Task" },
            new DataGridViewTextBoxColumn { HeaderText = "Son Güncelleme", Name = "Time" },
        ]);
        Controls.Add(_grid);

        SdlcRuntime.EventBus.Subscribe(SdlcEventType.AgentStateChanged, _ =>
        {
            if (IsDisposed) return;
            BeginInvoke(RefreshGrid);
        });

        Load += (_, _) => RefreshGrid();
    }

    private void RefreshGrid()
    {
        var snapshots = SdlcRuntime.AgentCoordinator.GetAllSnapshots();
        _grid.Rows.Clear();
        foreach (var s in snapshots)
            _grid.Rows.Add(s.Role, s.State, s.CurrentTaskId ?? "—", s.TimestampUtc.ToLocalTime().ToString("HH:mm:ss"));
    }
}
