namespace Assist.Forms.Productivity;

using Assist.Models;
using Assist.Services;

/// <summary>
/// Modal dialog for adding or editing a to-do item.
/// </summary>
internal sealed class TodoEditForm : Form
{
    private readonly TodoItem _item;
    private readonly bool     _isNew;

    private TextBox      _txtTitle    = null!;
    private TextBox      _txtDesc     = null!;
    private TextBox      _txtCategory = null!;
    private ComboBox     _cmbPriority = null!;
    private CheckBox     _chkDue      = null!;
    private DateTimePicker _dtp       = null!;

    public TodoEditForm(TodoItem? existing = null)
    {
        _isNew = existing is null;
        _item  = existing is not null ? ShallowClone(existing) : new TodoItem();
        BuildUI();
        LoadToUI();
        UITheme.Apply(this);
    }

    private static TodoItem ShallowClone(TodoItem s) => new()
    {
        Id          = s.Id,          Title       = s.Title,
        Description = s.Description, Category    = s.Category,
        Priority    = s.Priority,    DueDate     = s.DueDate,
        IsCompleted = s.IsCompleted, CreatedAt   = s.CreatedAt,
        CompletedAt = s.CompletedAt
    };

    private void BuildUI()
    {
        Text            = _isNew ? "➕ Yeni Görev" : "✏️ Görevi Düzenle";
        Size            = new Size(480, 320);
        MinimumSize     = new Size(420, 290);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;
        Font            = new Font("Consolas", 10);

        var layout = new TableLayoutPanel
        {
            Dock        = DockStyle.Fill,
            ColumnCount = 2,
            RowCount    = 6,
            Padding     = new Padding(16, 14, 16, 10),
        };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        for (int i = 0; i < 5; i++)
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 36));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100)); // button row

        _txtTitle    = MakeTxt();
        _txtDesc     = MakeTxt();
        _txtCategory = MakeTxt();

        _cmbPriority = new ComboBox
        {
            Dock          = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Font          = new Font("Consolas", 10),
            Margin        = new Padding(0, 3, 0, 3)
        };
        _cmbPriority.Items.AddRange(["⚪ Düşük", "🟡 Normal", "🟠 Yüksek", "🔴 Kritik"]);

        var duePanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
        _chkDue = new CheckBox
        {
            Text     = "Tarih belirle",
            Location = new Point(0, 8),
            AutoSize = true
        };
        _dtp = new DateTimePicker
        {
            Format   = DateTimePickerFormat.Short,
            Location = new Point(110, 5),
            Width    = 120,
            Value    = DateTime.Today.AddDays(1),
            Enabled  = false
        };
        _chkDue.CheckedChanged += (_, _) => _dtp.Enabled = _chkDue.Checked;
        duePanel.Controls.AddRange([_chkDue, _dtp]);

        layout.Controls.Add(MakeLbl("📝 Başlık *"),   0, 0); layout.Controls.Add(_txtTitle,    1, 0);
        layout.Controls.Add(MakeLbl("📋 Açıklama"),   0, 1); layout.Controls.Add(_txtDesc,     1, 1);
        layout.Controls.Add(MakeLbl("🏷️ Kategori"),   0, 2); layout.Controls.Add(_txtCategory, 1, 2);
        layout.Controls.Add(MakeLbl("⚡ Öncelik"),    0, 3); layout.Controls.Add(_cmbPriority, 1, 3);
        layout.Controls.Add(MakeLbl("📅 Bitiş"),      0, 4); layout.Controls.Add(duePanel,     1, 4);

        var btnBar = new FlowLayoutPanel
        {
            Dock            = DockStyle.Fill,
            FlowDirection   = FlowDirection.RightToLeft,
            WrapContents    = false,
            Padding         = new Padding(0, 6, 0, 0),
            BackColor       = Color.Transparent,
        };
        var btnSave   = MakeBtn("💾 Kaydet");
        btnSave.Click += OnSave;
        var btnCancel = MakeBtn("İptal");
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };
        btnBar.Controls.AddRange([btnCancel, btnSave]);

        layout.Controls.Add(btnBar, 0, 5);
        layout.SetColumnSpan(btnBar, 2);

        Controls.Add(layout);
        AcceptButton = btnSave;
        CancelButton = btnCancel;
    }

    private void LoadToUI()
    {
        _txtTitle.Text    = _item.Title;
        _txtDesc.Text     = _item.Description;
        _txtCategory.Text = _item.Category;
        _cmbPriority.SelectedIndex = (int)_item.Priority;
        if (_item.DueDate.HasValue)
        {
            _chkDue.Checked = true;
            _dtp.Value      = _item.DueDate.Value;
            _dtp.Enabled    = true;
        }
    }

    private void OnSave(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtTitle.Text))
        {
            MessageBox.Show("Başlık boş olamaz.", "Uyarı",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtTitle.Focus();
            return;
        }
        _item.Title       = _txtTitle.Text.Trim();
        _item.Description = _txtDesc.Text.Trim();
        _item.Category    = _txtCategory.Text.Trim();
        _item.Priority    = (TodoPriority)_cmbPriority.SelectedIndex;
        _item.DueDate     = _chkDue.Checked ? _dtp.Value.Date : null;

        if (_isNew) TodoStore.Add(_item);
        else        TodoStore.Update(_item);

        DialogResult = DialogResult.OK;
        Close();
    }

    private static TextBox MakeTxt() => new()
    {
        Dock        = DockStyle.Fill,
        Font        = new Font("Consolas", 10),
        BorderStyle = BorderStyle.FixedSingle,
        Margin      = new Padding(0, 3, 0, 3)
    };

    private static Label MakeLbl(string text) => new()
    {
        Text      = text,
        Dock      = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        Font      = new Font("Consolas", 10),
        Margin    = new Padding(0, 3, 6, 3)
    };

    private static Button MakeBtn(string text)
    {
        var btn = new Button
        {
            Text      = text,
            AutoSize  = true,
            Padding   = new Padding(14, 3, 14, 3),
            FlatStyle = FlatStyle.Flat,
            Font      = new Font("Consolas", 10),
            Margin    = new Padding(6, 0, 0, 0),
            Cursor    = Cursors.Hand
        };
        return btn;
    }
}
