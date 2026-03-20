using Microsoft.Win32;

namespace Assist;

/// <summary>
/// Manages Windows startup programs — list, enable, disable.
/// </summary>
public sealed class StartupManagerForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);

    private readonly ListView _listView = null!;
    private readonly Label _lblStatus = null!;

    private const string RunKey = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run";
    private const string DisabledSuffix = "_DISABLED";

    public StartupManagerForm()
    {
        Text = "Startup Manager";
        ClientSize = new Size(850, 550);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== STARTUP MANAGER ===",
            Location = new Point(20, 15),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        _listView = new ListView
        {
            Location = new Point(20, 55),
            Size = new Size(810, 400),
            View = View.Details,
            FullRowSelect = true,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle,
            GridLines = true,
            MultiSelect = false
        };
        _listView.Columns.Add("Durum", 80);
        _listView.Columns.Add("Ad", 200);
        _listView.Columns.Add("Komut", 500);

        var btnEnable = CreateButton("✅ Etkinleştir", new Point(20, 470));
        btnEnable.Click += (_, _) => SetEntryEnabled(true);

        var btnDisable = CreateButton("❌ Devre Dışı", new Point(200, 470));
        btnDisable.Click += (_, _) => SetEntryEnabled(false);

        var btnRefresh = CreateButton("🔄 Yenile", new Point(380, 470));
        btnRefresh.Click += (_, _) => LoadStartupItems();

        var btnDelete = CreateButton("🗑️ Sil", new Point(560, 470));
        btnDelete.ForeColor = Color.OrangeRed;
        btnDelete.FlatAppearance.BorderColor = Color.OrangeRed;
        btnDelete.Click += (_, _) => DeleteEntry();

        _lblStatus = new Label
        {
            Text = "",
            Location = new Point(20, 510),
            Width = 810,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9)
        };

        Controls.AddRange([lblTitle, _listView, btnEnable, btnDisable, btnRefresh, btnDelete, _lblStatus]);

        LoadStartupItems();
    }

    private void LoadStartupItems()
    {
        _listView.Items.Clear();

        LoadFromRegistry(Registry.CurrentUser, "HKCU");
        LoadFromRegistry(Registry.LocalMachine, "HKLM");

        _lblStatus.Text = $"Toplam {_listView.Items.Count} başlangıç öğesi bulundu.";
    }

    private void LoadFromRegistry(RegistryKey root, string rootLabel)
    {
        try
        {
            using var key = root.OpenSubKey(RunKey, false);
            if (key is null) return;

            foreach (var name in key.GetValueNames())
            {
                var value = key.GetValue(name)?.ToString() ?? "";
                var isDisabled = name.EndsWith(DisabledSuffix, StringComparison.OrdinalIgnoreCase);
                var displayName = isDisabled ? name[..^DisabledSuffix.Length] : name;

                var item = new ListViewItem(isDisabled ? "❌ Kapalı" : "✅ Açık");
                item.SubItems.Add($"[{rootLabel}] {displayName}");
                item.SubItems.Add(value);
                item.Tag = new StartupEntry(root, name, value, !isDisabled, rootLabel);
                item.ForeColor = isDisabled ? Color.Gray : GreenText;

                _listView.Items.Add(item);
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Kayıt defteri okunamadı ({rootLabel}): {ex.Message}";
        }
    }

    private void SetEntryEnabled(bool enable)
    {
        if (_listView.SelectedItems.Count == 0)
        {
            _lblStatus.Text = "Lütfen bir öğe seçin.";
            return;
        }

        if (_listView.SelectedItems[0].Tag is not StartupEntry entry)
        {
            _lblStatus.Text = "Geçersiz öğe.";
            return;
        }

        try
        {
            using var key = entry.Root.OpenSubKey(RunKey, true);
            if (key is null)
            {
                _lblStatus.Text = "Kayıt defteri anahtarı açılamadı. Yönetici olarak çalıştırmayı deneyin.";
                return;
            }

            if (enable && !entry.IsEnabled)
            {
                var originalName = entry.Name[..^DisabledSuffix.Length];
                key.SetValue(originalName, entry.Command);
                key.DeleteValue(entry.Name, false);
                _lblStatus.Text = $"'{originalName}' etkinleştirildi.";
            }
            else if (!enable && entry.IsEnabled)
            {
                var disabledName = entry.Name + DisabledSuffix;
                key.SetValue(disabledName, entry.Command);
                key.DeleteValue(entry.Name, false);
                _lblStatus.Text = $"'{entry.Name}' devre dışı bırakıldı.";
            }
            else
            {
                _lblStatus.Text = enable ? "Zaten etkin." : "Zaten devre dışı.";
                return;
            }

            LoadStartupItems();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Hata: {ex.Message}";
        }
    }

    private void DeleteEntry()
    {
        if (_listView.SelectedItems.Count == 0)
        {
            _lblStatus.Text = "Lütfen bir öğe seçin.";
            return;
        }

        if (_listView.SelectedItems[0].Tag is not StartupEntry entry)
        {
            _lblStatus.Text = "Geçersiz öğe.";
            return;
        }

        var confirm = MessageBox.Show(
            $"'{entry.Name}' başlangıç öğesini silmek istediğinize emin misiniz?",
            "Onay", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);

        if (confirm != DialogResult.Yes) return;

        try
        {
            using var key = entry.Root.OpenSubKey(RunKey, true);
            if (key is null)
            {
                _lblStatus.Text = "Kayıt defteri anahtarı açılamadı.";
                return;
            }

            key.DeleteValue(entry.Name, false);
            _lblStatus.Text = $"'{entry.Name}' silindi.";
            LoadStartupItems();
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Silme hatası: {ex.Message}";
        }
    }

    private static Button CreateButton(string text, Point location)
    {
        var btn = new Button
        {
            Text = text,
            Location = location,
            Width = 160,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btn.FlatAppearance.BorderColor = GreenText;
        return btn;
    }

    private sealed record StartupEntry(RegistryKey Root, string Name, string Command, bool IsEnabled, string RootLabel);
}
