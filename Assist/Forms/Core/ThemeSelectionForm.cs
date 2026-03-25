namespace Assist.Forms.Core;

using Assist.Services;

/// <summary>
/// Dialog for selecting and previewing application themes.
/// </summary>
internal sealed class ThemeSelectionForm : Form
{
    private readonly ListBox _lstThemes = new();
    private readonly Panel _preview = new();
    private readonly Label _lblSampleTitle = new();
    private readonly Label _lblSampleBody = new();
    private readonly Button _btnOk = new();
    private readonly Button _btnCancel = new();

    public AppTheme SelectedTheme { get; private set; } = ThemeService.CurrentTheme;

    public ThemeSelectionForm()
    {
        Text = "Tema Seçimi";
        StartPosition = FormStartPosition.CenterParent;
        Size = new Size(640, 380);
        MinimumSize = new Size(640, 380);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;

        BuildUi();
        ThemeService.ThemeChanged += OnThemeChanged;
        FormClosed += (_, _) => ThemeService.ThemeChanged -= OnThemeChanged;
        Load += (_, _) => RefreshSelection();
    }

    /// <summary>
    /// Builds the theme list, preview panel, and action buttons.
    /// </summary>
    private void BuildUi()
    {
        var left = new Panel
        {
            Dock = DockStyle.Left,
            Width = 220,
            Padding = new Padding(12),
        };

        var lbl = new Label
        {
            Text = "Temalar",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Consolas", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        left.Controls.Add(lbl);

        _lstThemes.Dock = DockStyle.Fill;
        _lstThemes.Font = new Font("Consolas", 10f);
        _lstThemes.IntegralHeight = false;
        _lstThemes.BorderStyle = BorderStyle.FixedSingle;
        _lstThemes.SelectedIndexChanged += (_, _) =>
        {
            if (_lstThemes.SelectedItem is ThemeItem item)
            {
                SelectedTheme = item.Theme;
                ApplyPreview(item.Theme);
            }
        };
        left.Controls.Add(_lstThemes);

        var right = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(12),
        };

        var previewTitle = new Label
        {
            Text = "Önizleme",
            Dock = DockStyle.Top,
            Height = 24,
            Font = new Font("Consolas", 11f, FontStyle.Bold),
            TextAlign = ContentAlignment.MiddleLeft,
        };
        right.Controls.Add(previewTitle);

        _preview.Dock = DockStyle.Fill;
        _preview.Padding = new Padding(16);
        _preview.BorderStyle = BorderStyle.FixedSingle;
        _preview.Controls.Add(_lblSampleBody);
        _preview.Controls.Add(_lblSampleTitle);
        right.Controls.Add(_preview);

        _lblSampleTitle.AutoSize = true;
        _lblSampleTitle.Font = new Font("Consolas", 14f, FontStyle.Bold);
        _lblSampleTitle.Location = new Point(16, 18);

        _lblSampleBody.AutoSize = true;
        _lblSampleBody.Font = new Font("Consolas", 10f);
        _lblSampleBody.Location = new Point(16, 58);
        _lblSampleBody.Text = "Bu tema tüm ekranlara uygulanacak.";

        var btnFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            AutoSize = true,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            Margin = new Padding(0),
            Padding = new Padding(0),
        };

        _btnCancel.Text = "İptal";
        _btnCancel.Width = 110;
        _btnCancel.Height = 34;
        _btnCancel.Margin = new Padding(6, 0, 0, 0);
        _btnCancel.FlatStyle = FlatStyle.Flat;
        _btnCancel.FlatAppearance.BorderSize = 1;
        _btnCancel.UseVisualStyleBackColor = false;
        _btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _btnOk.Text = "✔ Uygula";
        _btnOk.Width = 110;
        _btnOk.Height = 34;
        _btnOk.Margin = new Padding(6, 0, 12, 0);
        _btnOk.FlatStyle = FlatStyle.Flat;
        _btnOk.FlatAppearance.BorderSize = 1;
        _btnOk.UseVisualStyleBackColor = false;
        _btnOk.Click += (_, _) =>
        {
            if (_lstThemes.SelectedItem is ThemeItem item)
            {
                ThemeService.SetTheme(item.Theme);
                DialogResult = DialogResult.OK;
                Close();
            }
        };

        btnFlow.Controls.Add(_btnCancel);
        btnFlow.Controls.Add(_btnOk);

        var bottom = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 54,
            Padding = new Padding(0, 10, 0, 10),
        };
        bottom.Controls.Add(btnFlow);

        // Bottom must be added BEFORE Fill so dock layout reserves its space
        Controls.Add(bottom);
        Controls.Add(right);
        Controls.Add(left);

        foreach (var item in ThemeService.GetThemeOptions())
            _lstThemes.Items.Add(new ThemeItem(item.Theme, item.Name));

        UITheme.Apply(this);
        ApplyPreview(ThemeService.CurrentTheme);
        RefreshSelection();
    }

    /// <summary>
    /// Selects the currently active theme in the list box.
    /// </summary>
    private void RefreshSelection()
    {
        for (int i = 0; i < _lstThemes.Items.Count; i++)
        {
            if (_lstThemes.Items[i] is ThemeItem item && item.Theme == ThemeService.CurrentTheme)
            {
                _lstThemes.SelectedIndex = i;
                return;
            }
        }
    }

    /// <summary>
    /// Updates the preview panel colors and labels to reflect the selected theme.
    /// </summary>
    private void ApplyPreview(AppTheme theme)
    {
        var p = ThemeService.GetPalette(theme);
        _preview.BackColor = p.Surface;
        _preview.ForeColor = p.Text;
        _lblSampleTitle.ForeColor = p.Accent;
        _lblSampleBody.ForeColor = p.Text;
        _lblSampleTitle.Text = $"{GetThemeName(theme)}";
        _lblSampleBody.Text = "Bu tema seçildiğinde açık tüm ekranlara uygulanır.";
        BackColor = p.Back;
        ForeColor = p.Text;
    }

    /// <summary>
    /// Returns the display name for the specified theme.
    /// </summary>
    private static string GetThemeName(AppTheme theme)
        => ThemeService.GetThemeOptions().First(x => x.Theme == theme).Name;

    /// <summary>
    /// Refreshes the selected item in the list when the theme changes externally.
    /// </summary>
    private void OnThemeChanged(object? sender, EventArgs e)
    {
        if (IsDisposed) return;
        BeginInvoke(new Action(RefreshSelection));
    }

    private sealed record ThemeItem(AppTheme Theme, string Name)
    {
        public override string ToString() => Name;
    }
}
