namespace Assist.Forms.SystemTools;

using System.Diagnostics;
using System.Text.RegularExpressions;
using Assist.Services;

internal sealed partial class WifiPasswordForm : Form
{

    private readonly DataGridView _dgv;
    private readonly Label _lblStatus;

    public WifiPasswordForm()
    {
        Text = "Wi-Fi Şifreleri";
        ClientSize = new Size(860, 520);
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Black };

        var btnRefresh = new Button
        {
            Text = "Yenile",
            Location = new Point(6, 7),
            Width = 140,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };
        btnRefresh.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnRefresh.Click += async (_, _) => await LoadProfilesAsync();

        var btnCopy = new Button
        {
            Text = "Seçili Şifreyi Kopyala",
            Location = new Point(156, 7),
            Width = 220,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };
        btnCopy.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnCopy.Click += (_, _) => CopySelectedPassword();

        _lblStatus = new Label
        {
            Text = "",
            Location = new Point(390, 12),
            AutoSize = true,
            ForeColor = Color.Cyan
        };

        topPanel.Controls.AddRange([btnRefresh, btnCopy, _lblStatus]);

        _dgv = CreateGrid();

        Controls.Add(_dgv);
        Controls.Add(topPanel);

        Load += async (_, _) => await LoadProfilesAsync();
    }

    [GeneratedRegex(@"(?:All User Profile|Tüm Kullanıcı Profili)\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex ProfileNameRegex();

    [GeneratedRegex(@"(?:Key Content|Anahtar [İi]çeri[ğg]i)\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex KeyContentRegex();

    [GeneratedRegex(@"(?:Authentication|Kimlik\s+Do[ğg]rulama\S*)\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex AuthRegex();

    [GeneratedRegex(@"(?:Cipher|Şifre)\s*:\s*(.+)", RegexOptions.IgnoreCase)]
    private static partial Regex CipherRegex();

    private async Task LoadProfilesAsync()
    {
        await Loading.RunAsync(this, async () =>
        {
            var profilesOutput = await RunNetshAsync("wlan show profiles");
            var profileNames = ProfileNameRegex()
                .Matches(profilesOutput)
                .Select(m => m.Groups[1].Value.Trim())
                .ToList();

            _dgv.Rows.Clear();

            foreach (var name in profileNames)
            {
                var detail = await RunNetshAsync($"wlan show profile name=\"{name}\" key=clear");

                var key = KeyContentRegex().Match(detail);
                var auth = AuthRegex().Match(detail);
                var cipher = CipherRegex().Match(detail);

                var password = key.Success ? key.Groups[1].Value.Trim() : "(gösterilemiyor)";
                var authType = auth.Success ? auth.Groups[1].Value.Trim() : "-";
                var cipherType = cipher.Success ? cipher.Groups[1].Value.Trim() : "-";

                _dgv.Rows.Add(name, password, authType, cipherType);
            }

            _lblStatus.Text = $"{profileNames.Count} profil bulundu";
        }, "Wi-Fi profilleri taranıyor...");
    }

    private void CopySelectedPassword()
    {
        if (_dgv.CurrentRow is null) return;
        var pwd = _dgv.CurrentRow.Cells["Password"].Value?.ToString();
        if (!string.IsNullOrEmpty(pwd) && pwd != "(gösterilemiyor)")
        {
            Clipboard.SetText(pwd);
            _lblStatus.Text = "Şifre panoya kopyalandı!";
        }
    }

    private static async Task<string> RunNetshAsync(string args)
    {
        // chcp 65001 forces UTF-8 output before netsh runs.
        // This avoids System.NotSupportedException for OEM code pages (e.g. 857 Turkish)
        // that are not available in .NET's default encoding set.
        using var proc = new Process();
        proc.StartInfo = new ProcessStartInfo("cmd.exe", $"/c chcp 65001 >nul && netsh {args}")
        {
            RedirectStandardOutput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = System.Text.Encoding.UTF8
        };
        proc.Start();
        var output = await proc.StandardOutput.ReadToEndAsync();
        await proc.WaitForExitAsync();
        return output;
    }

    private static DataGridView CreateGrid()
    {
        var dgv = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            BackgroundColor = Color.Black,
            GridColor = Color.FromArgb(0, 80, 0),
            DefaultCellStyle = { BackColor = Color.Black, ForeColor = Color.FromArgb(0, 255, 0), SelectionBackColor = Color.FromArgb(0, 60, 0) },
            ColumnHeadersDefaultCellStyle = { BackColor = Color.FromArgb(20, 20, 20), ForeColor = Color.FromArgb(0, 255, 0), Font = new Font("Consolas", 10, FontStyle.Bold) },
            EnableHeadersVisualStyles = false,
            BorderStyle = BorderStyle.None
        };
        dgv.RowTemplate.Height = 28;

        dgv.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { Name = "ProfileName", HeaderText = "Profil Adı", FillWeight = 30 },
            new DataGridViewTextBoxColumn { Name = "Password", HeaderText = "Şifre", FillWeight = 30 },
            new DataGridViewTextBoxColumn { Name = "Auth", HeaderText = "Doğrulama", FillWeight = 20 },
            new DataGridViewTextBoxColumn { Name = "Cipher", HeaderText = "Şifreleme", FillWeight = 20 }
        ]);

        return dgv;
    }
}
