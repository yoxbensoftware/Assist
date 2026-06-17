namespace Assist.Forms.Core;

using Assist.Services;

internal sealed class QuickLauncherForm : Form
{
    private readonly IReadOnlyList<QuickLaunchItem> _items;
    private readonly TextBox _txtSearch = new();
    private readonly ListBox _lstResults = new();

    public QuickLauncherForm(IReadOnlyList<QuickLaunchItem> items)
    {
        _items = items;
        Text = "Hızlı Başlatıcı";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(620, 420);
        MinimumSize = new Size(520, 340);
        KeyPreview = true;

        BuildUi();
        UITheme.Apply(this);
        Load += (_, _) =>
        {
            ApplyFilter();
            _txtSearch.Focus();
        };
        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Escape)
                Close();
        };
    }

    private void BuildUi()
    {
        var p = UITheme.Palette;
        Padding = new Padding(14);

        var title = new Label
        {
            Text = "Ctrl+K",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Consolas", 12, FontStyle.Bold),
            ForeColor = p.Accent
        };

        _txtSearch.Dock = DockStyle.Top;
        _txtSearch.Height = 32;
        _txtSearch.PlaceholderText = "Araç adı yazın...";
        _txtSearch.TextChanged += (_, _) => ApplyFilter();
        _txtSearch.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Down && _lstResults.Items.Count > 0)
            {
                _lstResults.Focus();
                _lstResults.SelectedIndex = Math.Max(0, _lstResults.SelectedIndex);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Enter)
            {
                ExecuteSelected();
                e.Handled = true;
            }
        };

        _lstResults.Dock = DockStyle.Fill;
        _lstResults.IntegralHeight = false;
        _lstResults.DoubleClick += (_, _) => ExecuteSelected();
        _lstResults.KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.Enter)
            {
                ExecuteSelected();
                e.Handled = true;
            }
        };

        Controls.Add(_lstResults);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
        Controls.Add(_txtSearch);
        Controls.Add(new Panel { Dock = DockStyle.Top, Height = 10 });
        Controls.Add(title);
    }

    private void ApplyFilter()
    {
        var query = _txtSearch.Text.Trim();
        var filtered = string.IsNullOrWhiteSpace(query)
            ? _items
            : _items.Where(item =>
                    item.Title.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Category.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    item.Keywords.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToList();

        _lstResults.BeginUpdate();
        try
        {
            _lstResults.Items.Clear();
            foreach (var item in filtered.Take(40))
                _lstResults.Items.Add(item);

            if (_lstResults.Items.Count > 0)
                _lstResults.SelectedIndex = 0;
        }
        finally
        {
            _lstResults.EndUpdate();
        }
    }

    private void ExecuteSelected()
    {
        if (_lstResults.SelectedItem is not QuickLaunchItem item)
            return;

        Close();
        item.Execute();
    }
}
