using Assist.Models;
using Assist.Services;

namespace Assist.Forms.Passwords;

internal partial class PasswordListForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);

    private DataGridView _dgv = null!;
    private TextBox _txtSearch = null!;
    private List<PasswordEntry> _allEntries = [];

    public PasswordListForm()
    {
        InitializeComponent();
        InitializeDataGridView();
        Load += (_, _) => RefreshList();
    }

    private void InitializeDataGridView()
    {
        Size = new Size(900, 500);
        MinimumSize = new Size(800, 400);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        // Find existing DataGridView from designer
        _dgv = Controls.OfType<DataGridView>().First();
        _dgv.Dock = DockStyle.Fill;
        _dgv.ReadOnly = false;
        _dgv.EditMode = DataGridViewEditMode.EditOnEnter;
        UITheme.ApplyDark(_dgv);

        // Add search box
        _txtSearch = new TextBox
        {
            Dock = DockStyle.Top,
            PlaceholderText = "Ara (başlık, kullanıcı, not)...",
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10)
        };
        _txtSearch.TextChanged += (_, _) => ApplyFilter();
        Controls.Add(_txtSearch);
        Controls.SetChildIndex(_dgv, 0);

        ConfigureColumns();
        AddActionColumns();

        _dgv.CellContentClick += OnCellContentClick;
    }

    private void ConfigureColumns()
    {
        var titleCol = _dgv.Columns[AppConstants.ColumnTitle];
        if (titleCol is not null)
            titleCol.ReadOnly = true;

        var usernameCol = _dgv.Columns[AppConstants.ColumnUsername];
        if (usernameCol is not null)
            usernameCol.ReadOnly = true;

        var notesCol = _dgv.Columns[AppConstants.ColumnNotes];
        if (notesCol is not null)
            notesCol.ReadOnly = true;

        var passwordCol = _dgv.Columns[AppConstants.ColumnPassword];
        if (passwordCol is not null)
            passwordCol.ReadOnly = false;

        if (_dgv.Columns[AppConstants.ColumnEye] is DataGridViewButtonColumn eyeColumn)
        {
            eyeColumn.UseColumnTextForButtonValue = true;
            eyeColumn.Text = AppConstants.IconEye;
        }
    }

    private void AddActionColumns()
    {
        if (!_dgv.Columns.Contains(AppConstants.ColumnEdit))
        {
            _dgv.Columns.Add(new DataGridViewButtonColumn
            {
                Name = AppConstants.ColumnEdit,
                HeaderText = "Düzenle",
                Text = AppConstants.IconEdit,
                UseColumnTextForButtonValue = true,
                Width = 60
            });
        }

        if (!_dgv.Columns.Contains(AppConstants.ColumnDelete))
        {
            _dgv.Columns.Add(new DataGridViewButtonColumn
            {
                Name = AppConstants.ColumnDelete,
                HeaderText = "Sil",
                Text = AppConstants.IconDelete,
                UseColumnTextForButtonValue = true,
                Width = 60
            });
        }
    }

    private void RefreshList()
    {
        PasswordStore.LoadFromFile();
        _allEntries = [.. PasswordStore.Entries];
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        try
        {
            var filter = _txtSearch?.Text?.Trim() ?? string.Empty;
            var filtered = string.IsNullOrEmpty(filter)
                ? _allEntries
                : _allEntries.Where(e =>
                    (e.Title?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Username?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (e.Notes?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false))
                    .ToList();

            _dgv.Rows.Clear();
            foreach (var entry in filtered)
            {
                AddRowToGrid(entry);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Liste güncellenirken hata: {ex.Message}",
                "Hata",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void AddRowToGrid(PasswordEntry entry)
    {
        var maskedPassword = new string('*', entry.GetDecryptedPassword().Length);
        var rowIndex = _dgv.Rows.Add(entry.Title, entry.Username, maskedPassword, entry.Notes);
        var row = _dgv.Rows[rowIndex];

        row.Cells[AppConstants.ColumnEye].Value = AppConstants.IconEye;
        row.Cells[AppConstants.ColumnEdit].Value = AppConstants.IconEdit;
        row.Cells[AppConstants.ColumnDelete].Value = AppConstants.IconDelete;
        row.Cells[AppConstants.ColumnPassword].ToolTipText = entry.GetDecryptedPassword();
        row.Cells[AppConstants.ColumnPassword].Tag = true; // masked
    }

    private void OnCellContentClick(object? sender, DataGridViewCellEventArgs e)
    {
        if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

        var columnName = _dgv.Columns[e.ColumnIndex].Name;

        switch (columnName)
        {
            case AppConstants.ColumnEye:
                ShowPassword(e.RowIndex);
                break;
            case AppConstants.ColumnEdit:
                EditPassword(e.RowIndex);
                break;
            case AppConstants.ColumnDelete:
                DeletePassword(e.RowIndex);
                break;
        }
    }

    private void ShowPassword(int rowIndex)
    {
        var password = _dgv.Rows[rowIndex].Cells[AppConstants.ColumnPassword].ToolTipText;
        MessageBox.Show(
            $"Şifre: {password}",
            "Şifreyi Göster",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private void EditPassword(int rowIndex)
    {
        var row = _dgv.Rows[rowIndex];
        var title = row.Cells[AppConstants.ColumnTitle].Value?.ToString();
        var username = row.Cells[AppConstants.ColumnUsername].Value?.ToString();
        var password = row.Cells[AppConstants.ColumnPassword].ToolTipText;
        var notes = row.Cells[AppConstants.ColumnNotes].Value?.ToString();

        using var editForm = new PasswordEditForm(title, username, password, notes);
        editForm.ShowDialog();
        RefreshList();
    }

    private void DeletePassword(int rowIndex)
    {
        var title = _dgv.Rows[rowIndex].Cells[AppConstants.ColumnTitle].Value?.ToString();

        var result = MessageBox.Show(
            $"'{title}' kaydını silmek istediğinize emin misiniz?",
            "Onay",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Warning);

        if (result == DialogResult.Yes)
        {
            PasswordStore.DeleteEntry(title);
            _dgv.Rows.RemoveAt(rowIndex);
        }
    }
}
