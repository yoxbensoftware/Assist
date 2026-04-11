namespace Assist.SDLC.Forms;

using Assist.SDLC.Services;

/// <summary>Waiting Queue Monitor — shows active waiting entries.</summary>
internal sealed class WaitingQueueForm : SdlcBaseForm
{
    private readonly DataGridView _grid;

    public WaitingQueueForm()
    {
        Text = "⏳ Waiting Queue Monitor";
        Size = new Size(820, 400);

        var top = new Panel { Dock = DockStyle.Top, Height = 42 };
        var btn = CreateButton("Yenile", 12, 6, 100, 28);
        btn.Click += (_, _) => RefreshGrid();
        top.Controls.Add(btn);
        Controls.Add(top);

        _grid = CreateGrid();
        _grid.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { HeaderText = "ID", Name = "Id", Width = 90 },
            new DataGridViewTextBoxColumn { HeaderText = "Agent", Name = "Agent", Width = 110 },
            new DataGridViewTextBoxColumn { HeaderText = "Neden", Name = "Reason" },
            new DataGridViewTextBoxColumn { HeaderText = "Başlangıç", Name = "Start", Width = 140 },
            new DataGridViewTextBoxColumn { HeaderText = "Max Bekleme", Name = "Max", Width = 100 },
        ]);
        Controls.Add(_grid);

        Load += (_, _) => RefreshGrid();
    }

    private void RefreshGrid()
    {
        var entries = SdlcRuntime.WaitingManager.GetActive();
        _grid.Rows.Clear();
        foreach (var e in entries)
            _grid.Rows.Add(e.Id, e.WaitingAgent?.ToString() ?? "—", e.Reason,
                e.StartedUtc.ToLocalTime().ToString("g"), $"{e.MaxWait.TotalMinutes:F0} dk");
    }
}
