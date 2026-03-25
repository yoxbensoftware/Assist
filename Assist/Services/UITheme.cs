namespace Assist.Services;

/// <summary>
/// Theme-aware WinForms styling helper.
/// </summary>
internal static class UITheme
{
    /// <summary>
    /// Gets the current theme's color palette.
    /// </summary>
    public static ThemePalette Palette => ThemeService.GetPalette(ThemeService.CurrentTheme);

    /// <summary>
    /// Applies the current theme to all currently open forms.
    /// </summary>
    public static void ApplyToOpenForms()
    {
        foreach (Form form in Application.OpenForms)
            Apply(form);
    }

    /// <summary>
    /// Applies the current theme to a control and all its children recursively.
    /// </summary>
    public static void Apply(Control? root)
    {
        if (root is null) return;

        ApplyCore(root, Palette);
    }

    /// <summary>
    /// Applies the current theme styling to a <see cref="DataGridView"/> control.
    /// </summary>
    public static void Apply(DataGridView? dgv)
    {
        if (dgv is null) return;

        var p = Palette;
        dgv.BackgroundColor = p.Back;
        dgv.GridColor = p.Grid;
        dgv.EnableHeadersVisualStyles = false;

        dgv.ColumnHeadersDefaultCellStyle.BackColor = p.Surface2;
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = p.Text;
        dgv.DefaultCellStyle.BackColor = p.Surface;
        dgv.DefaultCellStyle.ForeColor = p.Text;
        dgv.RowTemplate.DefaultCellStyle.BackColor = p.Surface;
        dgv.RowTemplate.DefaultCellStyle.ForeColor = p.Text;
        dgv.RowHeadersDefaultCellStyle.BackColor = p.Surface2;
        dgv.RowHeadersDefaultCellStyle.ForeColor = p.Text;
    }

    /// <summary>
    /// Applies the current theme styling to a <see cref="ListView"/> control.
    /// </summary>
    public static void Apply(ListView? lv)
    {
        if (lv is null) return;

        var p = Palette;
        lv.BackColor = p.Back;
        lv.ForeColor = p.Text;
        lv.FullRowSelect = true;
    }

    /// <summary>
    /// Applies the current theme styling to a <see cref="TextBox"/> control.
    /// </summary>
    public static void Apply(TextBox? txt)
    {
        if (txt is null) return;

        var p = Palette;
        txt.BackColor = p.Surface;
        txt.ForeColor = p.Text;
    }

    /// <summary>
    /// Applies the current theme styling to a <see cref="Form"/>, including font and child controls.
    /// </summary>
    public static void Apply(Form? form)
    {
        if (form is null) return;

        var p = Palette;
        form.BackColor = p.Back;
        form.ForeColor = p.Text;
        form.Font = new Font("Consolas", 10);

        ApplyCore(form, p);
    }

    /// <summary>
    /// Applies the current theme text color to a <see cref="Label"/> control.
    /// </summary>
    public static void Apply(Label? lbl)
    {
        if (lbl is null) return;
        lbl.ForeColor = Palette.Text;
    }

    /// <summary>
    /// Applies the current theme styling to a <see cref="Button"/> with flat appearance.
    /// </summary>
    public static void Apply(Button? btn)
    {
        if (btn is null) return;

        var p = Palette;
        btn.BackColor = p.Surface2;
        btn.ForeColor = p.Text;
        btn.FlatStyle = FlatStyle.Flat;
        btn.UseVisualStyleBackColor = false;
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = p.Accent;
    }

    /// <summary>
    /// Applies the current theme styling to a <see cref="MenuStrip"/> control.
    /// </summary>
    public static void Apply(MenuStrip? menu)
    {
        if (menu is null) return;

        var p = Palette;
        menu.BackColor = p.MenuBack;
        menu.ForeColor = p.Text;
    }

    /// <summary>
    /// Recursively applies theme colors and fonts to a control tree based on control type.
    /// </summary>
    private static void ApplyCore(Control root, ThemePalette p)
    {
        switch (root)
        {
            case Form form:
                form.BackColor = p.Back;
                form.ForeColor = p.Text;
                form.Font = new Font("Consolas", 10);
                break;
            case MenuStrip menu:
                menu.BackColor = p.MenuBack;
                menu.ForeColor = p.Text;
                break;
            case StatusStrip status:
                status.BackColor = p.MenuBack;
                status.ForeColor = p.Text;
                break;
            case ToolStrip toolStrip:
                toolStrip.BackColor = p.MenuBack;
                toolStrip.ForeColor = p.Text;
                break;
            case TabControl tabControl:
                tabControl.BackColor = p.Back;
                tabControl.ForeColor = p.Text;
                break;
            case TabPage tabPage:
                tabPage.BackColor = p.Back;
                tabPage.ForeColor = p.Text;
                break;
            case DataGridView dgv:
                Apply(dgv);
                break;
            case ListView lv:
                Apply(lv);
                break;
            case ListBox lb:
                lb.BackColor = p.Surface;
                lb.ForeColor = p.Text;
                break;
            case ProgressBar pb:
                pb.BackColor = p.Surface2;
                pb.ForeColor = p.Accent;
                break;
            case TextBox txt:
                Apply(txt);
                break;
            case RichTextBox rtb:
                rtb.BackColor = p.Surface;
                rtb.ForeColor = p.Text;
                break;
            case Label lbl:
                lbl.ForeColor = p.Text;
                break;
            case Button btn:
                Apply(btn);
                break;
            case Panel panel:
                // If this panel wraps a BorderStyle.None TextBox, match its surface color
                bool isTextBoxWrapper = panel.Controls.Count == 1 && panel.Controls[0] is TextBox { BorderStyle: BorderStyle.None };
                panel.BackColor = isTextBoxWrapper ? p.Surface
                    : panel.BackColor == Color.Empty || panel.BackColor == SystemColors.Control ? p.Back : panel.BackColor;
                break;
            case GroupBox groupBox:
                groupBox.BackColor = p.Back;
                groupBox.ForeColor = p.Text;
                break;
            case ComboBox comboBox:
                comboBox.BackColor = p.Surface;
                comboBox.ForeColor = p.Text;
                break;
            case NumericUpDown nud:
                nud.BackColor = p.Surface;
                nud.ForeColor = p.Text;
                break;
            case CheckBox checkBox:
                checkBox.ForeColor = p.Text;
                break;
            case RadioButton radioButton:
                radioButton.ForeColor = p.Text;
                break;
        }

        foreach (Control child in root.Controls)
            ApplyCore(child, p);
    }
}
