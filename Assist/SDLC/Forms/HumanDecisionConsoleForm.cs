namespace Assist.SDLC.Forms;

using Assist.Services;
using Assist.SDLC.Domain;
using Assist.SDLC.Services;

/// <summary>
/// Human Decision Console — approve/reject, override, re-route, manual notes.
/// </summary>
internal sealed class HumanDecisionConsoleForm : SdlcBaseForm
{
    private readonly DataGridView _grid;
    private readonly TextBox _txtNote;
    private readonly Button _btnApprove;
    private readonly Button _btnReject;
    private readonly Button _btnRetry;
    private readonly RichTextBox _log;

    public HumanDecisionConsoleForm()
    {
        Text = "🧑‍💼 Human Decision Console";
        Size = new Size(900, 600);

        // ── Pending approvals grid ────────────────────────
        _grid = CreateGrid(DockStyle.None);
        _grid.Location = new Point(12, 12);
        _grid.Size = new Size(860, 200);
        _grid.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { HeaderText = "ID", Name = "Id", Width = 90 },
            new DataGridViewTextBoxColumn { HeaderText = "Agent", Name = "Agent", Width = 110 },
            new DataGridViewTextBoxColumn { HeaderText = "Başlık", Name = "Title" },
            new DataGridViewTextBoxColumn { HeaderText = "Tarih", Name = "Date", Width = 140 },
        ]);
        Controls.Add(_grid);

        // ── Action bar ────────────────────────────────────
        int y = 220;
        Controls.Add(CreateLabel("Not:", 12, y));
        _txtNote = CreateTextBox(60, y, 400);
        Controls.Add(_txtNote);

        _btnApprove = CreateButton("✅ Onayla", 480, y, 110, 28);
        _btnReject = CreateButton("❌ Reddet", 598, y, 110, 28);
        _btnRetry = CreateButton("🔄 Tekrarla", 716, y, 110, 28);

        _btnApprove.Click += (_, _) => DecideSelected(HumanAction.Approve);
        _btnReject.Click += (_, _) => DecideSelected(HumanAction.Reject);
        _btnRetry.Click += (_, _) => DecideSelected(HumanAction.Retry);

        Controls.AddRange([_btnApprove, _btnReject, _btnRetry]);

        // ── Override log ──────────────────────────────────
        _log = CreateOutputBox(DockStyle.None);
        _log.Location = new Point(12, y + 40);
        _log.Size = new Size(860, 280);
        Controls.Add(_log);

        SdlcRuntime.Approvals.ApprovalRequested += (_, req) =>
        {
            if (IsDisposed) return;
            BeginInvoke(RefreshGrid);
        };

        Load += (_, _) => RefreshGrid();
    }

    private void RefreshGrid()
    {
        var pending = SdlcRuntime.Approvals.GetPending();
        _grid.Rows.Clear();
        foreach (var r in pending)
            _grid.Rows.Add(r.Id, r.RequestingAgent, r.Title, r.RequestedUtc.ToLocalTime().ToString("g"));
    }

    private void DecideSelected(HumanAction action)
    {
        if (_grid.CurrentRow is null) return;
        var id = _grid.CurrentRow.Cells["Id"].Value?.ToString();
        if (string.IsNullOrEmpty(id)) return;

        SdlcRuntime.Approvals.Decide(id, action, _txtNote.Text.Trim());
        AppendOutput(_log, $"[{DateTime.Now:HH:mm:ss}] {action} → {id}: {_txtNote.Text}");
        _txtNote.Clear();
        RefreshGrid();
    }
}
