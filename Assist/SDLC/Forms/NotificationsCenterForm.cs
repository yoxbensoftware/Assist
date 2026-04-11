namespace Assist.SDLC.Forms;

using Assist.Services;
using Assist.SDLC.Domain;
using Assist.SDLC.Services;

/// <summary>Notifications Center — lists all notifications with severity, read/unread.</summary>
internal sealed class NotificationsCenterForm : SdlcBaseForm
{
    private readonly DataGridView _grid;
    private readonly Button _btnRefresh;
    private readonly Button _btnMarkRead;

    public NotificationsCenterForm()
    {
        Text = "🔔 Notifications Center";
        Size = new Size(850, 440);

        var top = new Panel { Dock = DockStyle.Top, Height = 42 };
        _btnRefresh = CreateButton("Yenile", 12, 6, 100, 28);
        _btnMarkRead = CreateButton("Okundu", 120, 6, 100, 28);
        _btnRefresh.Click += (_, _) => RefreshGrid();
        _btnMarkRead.Click += (_, _) => MarkSelectedRead();
        top.Controls.AddRange([_btnRefresh, _btnMarkRead]);
        Controls.Add(top);

        _grid = CreateGrid();
        _grid.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { HeaderText = "ID", Name = "Id", Width = 90 },
            new DataGridViewTextBoxColumn { HeaderText = "Seviye", Name = "Severity", Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Başlık", Name = "Title" },
            new DataGridViewTextBoxColumn { HeaderText = "Özet", Name = "Summary" },
            new DataGridViewTextBoxColumn { HeaderText = "Okundu", Name = "Read", Width = 60 },
            new DataGridViewTextBoxColumn { HeaderText = "Zaman", Name = "Time", Width = 130 },
        ]);
        Controls.Add(_grid);

        SdlcRuntime.Notifications.NotificationReceived += (_, _) =>
        {
            if (IsDisposed) return;
            BeginInvoke(RefreshGrid);
        };

        Load += (_, _) => RefreshGrid();
    }

    private void RefreshGrid()
    {
        _grid.Rows.Clear();
        foreach (var n in SdlcRuntime.Notifications.GetAll().OrderByDescending(n => n.TimestampUtc))
            _grid.Rows.Add(n.Id, n.Severity, n.Title, n.Summary,
                n.IsRead ? "✓" : "", n.TimestampUtc.ToLocalTime().ToString("g"));
    }

    private void MarkSelectedRead()
    {
        if (_grid.CurrentRow is null) return;
        var id = _grid.CurrentRow.Cells["Id"].Value?.ToString();
        if (id is not null)
        {
            SdlcRuntime.Notifications.MarkRead(id);
            RefreshGrid();
        }
    }
}
