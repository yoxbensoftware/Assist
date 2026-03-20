namespace Assist.Services;

/// <summary>
/// Centralized dark theme helper for UI controls.
/// Old school green-on-black terminal style.
/// </summary>
internal static class UITheme
{
    private static readonly Color BackgroundDark = Color.FromArgb(0, 0, 0);
    private static readonly Color BackgroundMedium = Color.FromArgb(10, 10, 10);
    private static readonly Color BackgroundHeader = Color.FromArgb(20, 20, 20);
    private static readonly Color GridLine = Color.FromArgb(0, 60, 0);
    private static readonly Color TextGreen = Color.FromArgb(0, 255, 0);

    public static void ApplyDark(DataGridView? dgv)
    {
        if (dgv is null) return;

        dgv.BackgroundColor = BackgroundDark;
        dgv.GridColor = GridLine;
        dgv.EnableHeadersVisualStyles = false;

        dgv.ColumnHeadersDefaultCellStyle.BackColor = BackgroundHeader;
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = TextGreen;

        dgv.DefaultCellStyle.BackColor = BackgroundMedium;
        dgv.DefaultCellStyle.ForeColor = TextGreen;

        dgv.RowTemplate.DefaultCellStyle.BackColor = BackgroundMedium;
        dgv.RowTemplate.DefaultCellStyle.ForeColor = TextGreen;

        dgv.RowHeadersDefaultCellStyle.BackColor = BackgroundHeader;
        dgv.RowHeadersDefaultCellStyle.ForeColor = TextGreen;
    }

    public static void ApplyDark(ListView? lv)
    {
        if (lv is null) return;

        lv.BackColor = BackgroundDark;
        lv.ForeColor = TextGreen;
        lv.FullRowSelect = true;
    }

    public static void ApplyDark(TextBox? txt)
    {
        if (txt is null) return;

        txt.BackColor = BackgroundMedium;
        txt.ForeColor = TextGreen;
    }

    public static void ApplyDark(Form? form)
    {
        if (form is null) return;

        form.BackColor = Color.Black;
        form.ForeColor = TextGreen;
    }

    public static void ApplyDark(Label? lbl)
    {
        if (lbl is null) return;

        lbl.ForeColor = TextGreen;
    }

    public static void ApplyDark(Button? btn)
    {
        if (btn is null) return;

        btn.BackColor = BackgroundHeader;
        btn.ForeColor = TextGreen;
        btn.FlatStyle = FlatStyle.Flat;
        btn.FlatAppearance.BorderColor = TextGreen;
    }

    public static void ApplyDark(MenuStrip? menu)
    {
        if (menu is null) return;

        menu.BackColor = BackgroundDark;
        menu.ForeColor = TextGreen;
    }
}
