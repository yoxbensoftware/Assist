namespace Assist.Forms.SystemTools;

/// <summary>
/// Editor for the Windows hosts file with syntax highlighting.
/// </summary>
internal sealed class HostsFileEditorForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly string HostsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.System),
        "drivers", "etc", "hosts");

    private readonly RichTextBox _txtEditor = null!;
    private readonly Label _lblStatus = null!;
    private readonly TextBox _txtIp = null!;
    private readonly TextBox _txtHostname = null!;

    public HostsFileEditorForm()
    {
        Text = "Hosts File Editor";
        ClientSize = new Size(800, 600);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== HOSTS FILE EDITOR ===",
            Location = new Point(20, 15),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        var lblPath = new Label
        {
            Text = $"Dosya: {HostsPath}",
            Location = new Point(20, 42),
            AutoSize = true,
            ForeColor = Color.Gray,
            Font = new Font("Consolas", 8)
        };

        _txtEditor = new RichTextBox
        {
            Location = new Point(20, 65),
            Size = new Size(760, 370),
            BackColor = Color.FromArgb(10, 10, 10),
            ForeColor = GreenText,
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle,
            WordWrap = false,
            AcceptsTab = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right | AnchorStyles.Bottom
        };
        _txtEditor.TextChanged += (_, _) => ApplySyntaxHighlighting();

        // Quick-add panel
        var lblQuickAdd = new Label
        {
            Text = "Hızlı Ekle:",
            Location = new Point(20, 445),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };

        var lblIp = new Label
        {
            Text = "IP:",
            Location = new Point(20, 475),
            Width = 30,
            ForeColor = GreenText
        };

        _txtIp = new TextBox
        {
            Location = new Point(55, 472),
            Width = 180,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle,
            Text = "127.0.0.1"
        };

        var lblHost = new Label
        {
            Text = "Hostname:",
            Location = new Point(250, 475),
            Width = 90,
            ForeColor = GreenText
        };

        _txtHostname = new TextBox
        {
            Location = new Point(345, 472),
            Width = 250,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 10),
            BorderStyle = BorderStyle.FixedSingle
        };

        var btnAdd = CreateButton("➕ Ekle", new Point(610, 470));
        btnAdd.Click += (_, _) => AddEntry();

        // Bottom buttons
        var btnSave = CreateButton("💾 Kaydet", new Point(20, 515));
        btnSave.Click += (_, _) => SaveHostsFile();

        var btnReload = CreateButton("🔄 Yenile", new Point(200, 515));
        btnReload.Click += (_, _) => LoadHostsFile();

        var btnBackup = CreateButton("📋 Yedekle", new Point(380, 515));
        btnBackup.Click += (_, _) => BackupHostsFile();

        var btnFlushDns = CreateButton("🌐 DNS Flush", new Point(560, 515));
        btnFlushDns.Click += (_, _) => FlushDns();

        _lblStatus = new Label
        {
            Text = "",
            Location = new Point(20, 560),
            Width = 760,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9)
        };

        Controls.AddRange([
            lblTitle, lblPath, _txtEditor,
            lblQuickAdd, lblIp, _txtIp, lblHost, _txtHostname, btnAdd,
            btnSave, btnReload, btnBackup, btnFlushDns,
            _lblStatus
        ]);

        LoadHostsFile();
    }

    private void LoadHostsFile()
    {
        try
        {
            if (!File.Exists(HostsPath))
            {
                _lblStatus.Text = "Hosts dosyası bulunamadı.";
                return;
            }

            _txtEditor.Text = File.ReadAllText(HostsPath);
            _lblStatus.Text = $"Dosya yüklendi: {HostsPath}";
        }
        catch (UnauthorizedAccessException)
        {
            _lblStatus.Text = "⚠ Okuma yetkisi yok. Uygulamayı yönetici olarak çalıştırın.";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Hata: {ex.Message}";
        }
    }

    private void SaveHostsFile()
    {
        try
        {
            File.WriteAllText(HostsPath, _txtEditor.Text);
            _lblStatus.Text = "✅ Hosts dosyası kaydedildi.";
        }
        catch (UnauthorizedAccessException)
        {
            _lblStatus.Text = "⚠ Yazma yetkisi yok. Uygulamayı yönetici olarak çalıştırın.";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Kaydetme hatası: {ex.Message}";
        }
    }

    private void BackupHostsFile()
    {
        try
        {
            var backupPath = HostsPath + $".backup_{DateTime.Now:yyyyMMdd_HHmmss}";
            File.Copy(HostsPath, backupPath, false);
            _lblStatus.Text = $"✅ Yedeklendi: {backupPath}";
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"Yedekleme hatası: {ex.Message}";
        }
    }

    private void AddEntry()
    {
        var ip = _txtIp.Text.Trim();
        var hostname = _txtHostname.Text.Trim();

        if (string.IsNullOrEmpty(ip) || string.IsNullOrEmpty(hostname))
        {
            _lblStatus.Text = "IP ve hostname alanları dolu olmalı.";
            return;
        }

        var line = $"{ip}\t{hostname}";
        _txtEditor.AppendText(Environment.NewLine + line);
        _txtHostname.Clear();
        _lblStatus.Text = $"Eklendi: {line}";
    }

    private void FlushDns()
    {
        try
        {
            using var process = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "ipconfig",
                Arguments = "/flushdns",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            });

            if (process is not null)
            {
                var output = process.StandardOutput.ReadToEnd();
                process.WaitForExit(10_000);
                _lblStatus.Text = "✅ DNS cache temizlendi.";
            }
        }
        catch (Exception ex)
        {
            _lblStatus.Text = $"DNS flush hatası: {ex.Message}";
        }
    }

    private void ApplySyntaxHighlighting()
    {
        var selStart = _txtEditor.SelectionStart;
        var selLength = _txtEditor.SelectionLength;

        _txtEditor.SuspendLayout();

        _txtEditor.SelectAll();
        _txtEditor.SelectionColor = GreenText;

        var lines = _txtEditor.Text.Split('\n');
        var pos = 0;

        foreach (var line in lines)
        {
            var trimmed = line.TrimStart();

            if (trimmed.StartsWith('#'))
            {
                _txtEditor.Select(pos, line.Length);
                _txtEditor.SelectionColor = Color.Gray;
            }

            pos += line.Length + 1; // +1 for \n
        }

        _txtEditor.Select(selStart, selLength);
        _txtEditor.ResumeLayout();
    }

    private static Button CreateButton(string text, Point location)
    {
        var btn = new Button
        {
            Text = text,
            Location = location,
            Width = 160,
            Height = 32,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 9, FontStyle.Bold)
        };
        btn.FlatAppearance.BorderColor = GreenText;
        return btn;
    }
}
