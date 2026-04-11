namespace Assist.SDLC.Forms;

using Assist.SDLC.Services;

/// <summary>Session / IDE Manager — lists open VS / VS Code instances.</summary>
internal sealed class SessionManagerForm : SdlcBaseForm
{
    private readonly DataGridView _grid;
    private readonly Button _btnRefresh;
    private readonly Button _btnLock;
    private readonly Button _btnUnlock;

    public SessionManagerForm()
    {
        Text = "💻 Session / IDE Manager";
        Size = new Size(800, 400);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 42 };
        _btnRefresh = CreateButton("Yenile", 12, 6, 100, 28);
        _btnLock = CreateButton("Kilitle", 120, 6, 100, 28);
        _btnUnlock = CreateButton("Kilidi Aç", 228, 6, 100, 28);

        _btnRefresh.Click += (_, _) => Refresh();
        _btnLock.Click += (_, _) => LockSelected();
        _btnUnlock.Click += (_, _) => UnlockSelected();

        topPanel.Controls.AddRange([_btnRefresh, _btnLock, _btnUnlock]);
        Controls.Add(topPanel);

        _grid = CreateGrid();
        _grid.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { HeaderText = "PID", Name = "PID", Width = 70 },
            new DataGridViewTextBoxColumn { HeaderText = "İşlem", Name = "Process", Width = 100 },
            new DataGridViewTextBoxColumn { HeaderText = "Pencere", Name = "Window" },
            new DataGridViewTextBoxColumn { HeaderText = "Kilitli", Name = "Locked", Width = 70 },
        ]);
        Controls.Add(_grid);

        Load += (_, _) => Refresh();
    }

    private new void Refresh()
    {
        var sessions = SdlcRuntime.IdeSessions.DetectSessions();
        _grid.Rows.Clear();
        foreach (var s in sessions)
            _grid.Rows.Add(s.ProcessId, s.ProcessName, s.WindowTitle, s.IsLocked ? "✓" : "");
    }

    private void LockSelected()
    {
        if (_grid.CurrentRow is null) return;
        var pid = (int)_grid.CurrentRow.Cells["PID"].Value;
        SdlcRuntime.IdeSessions.Lock(pid);
        Refresh();
    }

    private void UnlockSelected()
    {
        if (_grid.CurrentRow is null) return;
        var pid = (int)_grid.CurrentRow.Cells["PID"].Value;
        SdlcRuntime.IdeSessions.Unlock(pid);
        Refresh();
    }
}
