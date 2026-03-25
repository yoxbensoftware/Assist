namespace Assist.Forms.SystemTools;

using System.Diagnostics;
using System.Text.RegularExpressions;
using Assist.Services;

internal sealed partial class NetworkScannerForm : Form
{

    private readonly DataGridView _dgv;
    private readonly Label _lblStatus;
    private readonly ComboBox _cmbFilter;

    public NetworkScannerForm()
    {
        Text = "Ağ Bağlantı Tarayıcı";
        ClientSize = new Size(960, 600);
        BackColor = Color.Black;
        ForeColor = AppConstants.AccentText;
        Font = new Font("Consolas", 10);

        var topPanel = new Panel { Dock = DockStyle.Top, Height = 44, BackColor = Color.Black };

        var btnRefresh = new Button
        {
            Text = "Tara",
            Location = new Point(6, 7),
            Width = 120,
            Height = 30,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 10, FontStyle.Bold)
        };
        btnRefresh.FlatAppearance.BorderColor = AppConstants.AccentText;
        btnRefresh.Click += async (_, _) => await ScanAsync();

        var lblFilter = new Label { Text = "Filtre:", Location = new Point(146, 12), AutoSize = true, ForeColor = AppConstants.AccentText };

        _cmbFilter = new ComboBox
        {
            Location = new Point(220, 8),
            Width = 160,
            DropDownStyle = ComboBoxStyle.DropDownList,
            BackColor = Color.Black,
            ForeColor = AppConstants.AccentText,
            FlatStyle = FlatStyle.Flat
        };
        _cmbFilter.Items.AddRange(["Tümü", "ESTABLISHED", "LISTENING", "TIME_WAIT", "CLOSE_WAIT"]);
        _cmbFilter.SelectedIndex = 0;
        _cmbFilter.SelectedIndexChanged += async (_, _) => await ScanAsync();

        _lblStatus = new Label { Text = "", Location = new Point(400, 12), AutoSize = true, ForeColor = Color.Cyan };

        topPanel.Controls.AddRange([btnRefresh, lblFilter, _cmbFilter, _lblStatus]);

        _dgv = CreateGrid();

        Controls.Add(_dgv);
        Controls.Add(topPanel);

        Load += async (_, _) => await ScanAsync();
    }

    [GeneratedRegex(@"\s+(TCP|UDP)\s+(\S+)\s+(\S+)\s+(\S+)?\s*(\d+)")]
    private static partial Regex NetstatLineRegex();

    private async Task ScanAsync()
    {
        await Loading.RunAsync(this, async () =>
        {
            using var proc = new Process();
            proc.StartInfo = new ProcessStartInfo("netstat", "-ano")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };
            proc.Start();
            var output = await proc.StandardOutput.ReadToEndAsync();
            await proc.WaitForExitAsync();

            var processCache = new Dictionary<int, string>();
            var filter = _cmbFilter.SelectedItem?.ToString();
            var lines = output.Split('\n');

            _dgv.Rows.Clear();

            int count = 0;
            foreach (var line in lines)
            {
                var match = NetstatLineRegex().Match(line);
                if (!match.Success) continue;

                var proto = match.Groups[1].Value;
                var local = match.Groups[2].Value;
                var remote = match.Groups[3].Value;
                var state = proto == "UDP" ? "-" : (match.Groups[4].Value.Trim());
                var pid = int.Parse(match.Groups[5].Value);

                if (filter is not null and not "Tümü" && !state.Equals(filter, StringComparison.OrdinalIgnoreCase))
                    continue;

                if (!processCache.TryGetValue(pid, out var procName))
                {
                    try { procName = Process.GetProcessById(pid).ProcessName; }
                    catch { procName = "-"; }
                    processCache[pid] = procName;
                }

                var rowIndex = _dgv.Rows.Add(proto, local, remote, state, pid, procName);
                var row = _dgv.Rows[rowIndex];

                // Color code by state
                row.DefaultCellStyle.ForeColor = state switch
                {
                    "ESTABLISHED" => AppConstants.AccentText,
                    "LISTENING" => Color.Cyan,
                    "TIME_WAIT" => Color.DarkGray,
                    "CLOSE_WAIT" => Color.Orange,
                    "SYN_SENT" => Color.Yellow,
                    _ => AppConstants.AccentText
                };

                count++;
            }

            _lblStatus.Text = $"Toplam: {count} bağlantı";
        }, "Ağ bağlantıları taranıyor...");
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
            DefaultCellStyle = { BackColor = Color.Black, ForeColor = AppConstants.AccentText, SelectionBackColor = Color.FromArgb(0, 60, 0) },
            ColumnHeadersDefaultCellStyle = { BackColor = Color.FromArgb(20, 20, 20), ForeColor = AppConstants.AccentText, Font = new Font("Consolas", 10, FontStyle.Bold) },
            EnableHeadersVisualStyles = false,
            BorderStyle = BorderStyle.None
        };
        dgv.RowTemplate.Height = 26;

        dgv.Columns.AddRange(
        [
            new DataGridViewTextBoxColumn { Name = "Proto", HeaderText = "Protokol", FillWeight = 8 },
            new DataGridViewTextBoxColumn { Name = "Local", HeaderText = "Yerel Adres", FillWeight = 22 },
            new DataGridViewTextBoxColumn { Name = "Remote", HeaderText = "Uzak Adres", FillWeight = 22 },
            new DataGridViewTextBoxColumn { Name = "State", HeaderText = "Durum", FillWeight = 14 },
            new DataGridViewTextBoxColumn { Name = "PID", HeaderText = "PID", FillWeight = 8 },
            new DataGridViewTextBoxColumn { Name = "Process", HeaderText = "İşlem", FillWeight = 16 }
        ]);

        return dgv;
    }
}
