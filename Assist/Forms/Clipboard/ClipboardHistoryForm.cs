namespace Assist.Forms.ClipboardTools;

using Assist.Services;

/// <summary>
/// Displays the clipboard history in a searchable data grid with copy-on-double-click support.
/// </summary>
internal sealed class ClipboardHistoryForm : Form
{

    private readonly ClipboardHistoryService _service;
    private DataGridView _dgv = null!;
    private TextBox _txtSearch = null!;
    private System.Windows.Forms.Timer? _refreshTimer;
    private List<string> _allItems = [];

    /// <summary>
    /// Initializes the clipboard history form with the specified service instance.
    /// </summary>
    public ClipboardHistoryForm(ClipboardHistoryService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        Text = "Pano Geçmişi";
        ClientSize = new Size(700, 400);
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);
        InitializeComponents();
        StartAutoRefresh();
    }

    /// <summary>
    /// Creates and configures the search panel and data grid view controls.
    /// </summary>
    private void InitializeComponents()
    {
        // Search panel
        var searchPanel = new Panel { Dock = DockStyle.Top, Height = 40, BackColor = Color.Black };
        var lblSearch = new Label
        {
            Text = "Ara:",
            Location = new Point(10, 12),
            AutoSize = true,
            ForeColor = AppConstants.AccentText
        };
        _txtSearch = new TextBox
        {
            Location = new Point(50, 8),
            Width = 300,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            Font = new Font("Consolas", 10)
        };
        _txtSearch.TextChanged += (_, _) => ApplyFilter();
        searchPanel.Controls.AddRange([lblSearch, _txtSearch]);

        // DataGridView
        _dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
        };
        _dgv.RowTemplate.Height = 28;
        UITheme.Apply(_dgv);
        _dgv.Columns.Add("Text", "Metin");

        _dgv.CellDoubleClick += OnCellDoubleClick;

        Controls.Add(_dgv);
        Controls.Add(searchPanel);
    }

    /// <summary>
    /// Copies the selected clipboard entry to the system clipboard on double-click.
    /// </summary>
    private void OnCellDoubleClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0) return;
        var val = _dgv.Rows[e.RowIndex].Cells[0].Value?.ToString();
        if (string.IsNullOrEmpty(val)) return;

        try
        {
            _service.NotifyClipboardSetByApp(val);
            Clipboard.SetText(val);
            MessageBox.Show("Seçili metin panoya kopyalandı.", "Bilgi", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch { /* Clipboard access failed */ }
    }

    /// <summary>
    /// Starts a timer that periodically refreshes the displayed clipboard history.
    /// </summary>
    private void StartAutoRefresh()
    {
        _refreshTimer = new System.Windows.Forms.Timer { Interval = 1000 };
        _refreshTimer.Tick += async (_, _) => await RefreshDataAsync();
        _refreshTimer.Start();
    }

    /// <summary>
    /// Retrieves the latest clipboard history items and updates the data grid.
    /// </summary>
    private async Task RefreshDataAsync()
    {
        try
        {
            var items = await Task.Run(() => _service.GetAll());
            if (IsDisposed) return;

            _allItems = items;
            ApplyFilter();
        }
        catch { /* Service unavailable */ }
    }

    /// <summary>
    /// Filters the displayed clipboard entries based on the search text.
    /// </summary>
    private void ApplyFilter()
    {
        var filter = _txtSearch.Text.Trim();
        var filtered = string.IsNullOrEmpty(filter)
            ? _allItems
            : _allItems.Where(x => x.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();

        _dgv.Rows.Clear();
        foreach (var item in filtered)
        {
            _dgv.Rows.Add(item);
        }
    }

    /// <summary>
    /// Stops and disposes the auto-refresh timer when the form is closed.
    /// </summary>
    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        base.OnFormClosed(e);
        _refreshTimer?.Stop();
        _refreshTimer?.Dispose();
    }
}
