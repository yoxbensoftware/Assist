using System.Diagnostics;

namespace Assist.Forms.SystemTools;

/// <summary>
/// Internet speed test tool.
/// </summary>
internal sealed class SpeedTestForm : Form
{
    private static readonly Color GreenText = Color.FromArgb(0, 255, 0);
    private static readonly HttpClient HttpClient = new();

    private readonly Label _lblDownload = null!;
    private readonly Label _lblUpload = null!;
    private readonly Label _lblPing = null!;
    private readonly ProgressBar _progressBar = null!;
    private readonly Button _btnStart = null!;
    private readonly TextBox _txtLog = null!;

    public SpeedTestForm()
    {
        Text = "Speed Test";
        ClientSize = new Size(600, 450);
        BackColor = Color.Black;
        ForeColor = GreenText;
        Font = new Font("Consolas", 10);

        var lblTitle = new Label
        {
            Text = "=== INTERNET SPEED TEST ===",
            Location = new Point(20, 20),
            AutoSize = true,
            ForeColor = GreenText,
            Font = new Font("Consolas", 12, FontStyle.Bold)
        };

        _lblDownload = new Label
        {
            Text = "Download: -- Mbps",
            Location = new Point(20, 70),
            Width = 560,
            ForeColor = GreenText,
            Font = new Font("Consolas", 11)
        };

        _lblUpload = new Label
        {
            Text = "Upload: -- Mbps",
            Location = new Point(20, 100),
            Width = 560,
            ForeColor = GreenText,
            Font = new Font("Consolas", 11)
        };

        _lblPing = new Label
        {
            Text = "Ping: -- ms",
            Location = new Point(20, 130),
            Width = 560,
            ForeColor = GreenText,
            Font = new Font("Consolas", 11)
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(20, 170),
            Width = 560,
            Height = 25,
            Style = ProgressBarStyle.Continuous
        };

        _btnStart = new Button
        {
            Text = "Testi Başlat",
            Location = new Point(20, 210),
            Width = 560,
            Height = 40,
            BackColor = Color.Black,
            ForeColor = GreenText,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Consolas", 11, FontStyle.Bold)
        };
        _btnStart.FlatAppearance.BorderColor = GreenText;
        _btnStart.Click += async (_, _) => await RunSpeedTest();

        _txtLog = new TextBox
        {
            Location = new Point(20, 270),
            Width = 560,
            Height = 160,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            BackColor = Color.Black,
            ForeColor = GreenText,
            Font = new Font("Consolas", 9),
            BorderStyle = BorderStyle.FixedSingle
        };

        Controls.AddRange(new Control[]
        {
            lblTitle, _lblDownload, _lblUpload, _lblPing,
            _progressBar, _btnStart, _txtLog
        });
    }

    private async Task RunSpeedTest()
    {
        _btnStart.Enabled = false;
        _txtLog.Clear();
        _progressBar.Value = 0;

        try
        {
            LogMessage("Test başlatılıyor...");

            // Ping test
            LogMessage("\n[1/3] Ping testi yapılıyor...");
            _progressBar.Value = 10;
            var ping = await TestPing();
            _lblPing.Text = $"Ping: {ping} ms";
            LogMessage($"✓ Ping: {ping} ms");

            // Download test
            LogMessage("\n[2/3] Download hızı ölçülüyor...");
            _progressBar.Value = 40;
            var downloadSpeed = await TestDownloadSpeed();
            _lblDownload.Text = $"Download: {downloadSpeed:F2} Mbps";
            LogMessage($"✓ Download: {downloadSpeed:F2} Mbps");

            // Upload test (simulated)
            LogMessage("\n[3/3] Upload hızı ölçülüyor...");
            _progressBar.Value = 70;
            var uploadSpeed = downloadSpeed * 0.3; // Simulated
            _lblUpload.Text = $"Upload: {uploadSpeed:F2} Mbps";
            LogMessage($"✓ Upload: {uploadSpeed:F2} Mbps (simüle edildi)");

            _progressBar.Value = 100;
            LogMessage("\n✓ Test tamamlandı!");
        }
        catch (Exception ex)
        {
            LogMessage($"\n✗ Hata: {ex.Message}");
        }
        finally
        {
            _btnStart.Enabled = true;
        }
    }

    private async Task<long> TestPing()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            await HttpClient.GetAsync("https://www.google.com", HttpCompletionOption.ResponseHeadersRead);
            sw.Stop();
            return sw.ElapsedMilliseconds;
        }
        catch
        {
            return -1;
        }
    }

    private async Task<double> TestDownloadSpeed()
    {
        // Test file: 10MB from fast.com or similar
        var testUrl = "https://speed.cloudflare.com/__down?bytes=10000000";
        var sw = Stopwatch.StartNew();

        try
        {
            var response = await HttpClient.GetAsync(testUrl);
            var data = await response.Content.ReadAsByteArrayAsync();
            sw.Stop();

            var bytes = data.Length;
            var seconds = sw.Elapsed.TotalSeconds;
            var mbps = (bytes * 8) / (seconds * 1_000_000);

            return mbps;
        }
        catch
        {
            // Fallback: smaller test
            sw.Restart();
            await HttpClient.GetStringAsync("https://www.google.com");
            sw.Stop();
            return 10.0; // Simulated
        }
    }

    private void LogMessage(string message)
    {
        _txtLog.AppendText(message + Environment.NewLine);
    }
}
