namespace Assist.SDLC.Forms;

using Assist.Services;
using Assist.SDLC.Domain;
using Assist.SDLC.Services;

/// <summary>Console Runner — execute dotnet/npm/docker commands, see stdout/stderr.</summary>
internal sealed class ConsoleRunnerForm : SdlcBaseForm
{
    private readonly TextBox _txtCommand;
    private readonly TextBox _txtWorkDir;
    private readonly Button _btnRun;
    private readonly Button _btnCancel;
    private readonly RichTextBox _output;
    private CancellationTokenSource? _cts;

    public ConsoleRunnerForm()
    {
        Text = "⚡ Console Runner";
        Size = new Size(860, 540);

        int y = 12;
        Controls.Add(CreateLabel("Komut:", 12, y));
        _txtCommand = CreateTextBox(110, y, 720);
        _txtCommand.Text = "dotnet build";
        Controls.Add(_txtCommand);
        y += 34;

        Controls.Add(CreateLabel("Çalışma Dizini:", 12, y));
        _txtWorkDir = CreateTextBox(110, y, 720);
        Controls.Add(_txtWorkDir);
        y += 34;

        _btnRun = CreateButton("▶  Çalıştır", 110, y, 130, 32);
        _btnCancel = CreateButton("⏹  İptal", 250, y, 130, 32);
        _btnCancel.Enabled = false;

        _btnRun.Click += OnRun;
        _btnCancel.Click += (_, _) => _cts?.Cancel();

        Controls.AddRange([_btnRun, _btnCancel]);
        y += 42;

        _output = CreateOutputBox(DockStyle.None);
        _output.Location = new Point(12, y);
        _output.Size = new Size(820, 400);
        _output.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
        Controls.Add(_output);
    }

    private async void OnRun(object? sender, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_txtCommand.Text)) return;

        _cts = new CancellationTokenSource();
        _btnRun.Enabled = false;
        _btnCancel.Enabled = true;
        _output.Clear();

        var request = new ConsoleCommandRequest
        {
            Command = _txtCommand.Text.Trim(),
            WorkingDirectory = string.IsNullOrWhiteSpace(_txtWorkDir.Text) ? null : _txtWorkDir.Text.Trim(),
            Timeout = TimeSpan.FromMinutes(5)
        };

        try
        {
            var result = await SdlcRuntime.ConsoleCommand.RunAsync(request, _cts.Token);

            AppendOutput(_output, $"─── stdout ───");
            AppendOutput(_output, result.Stdout);
            if (!string.IsNullOrWhiteSpace(result.Stderr))
            {
                AppendOutput(_output, $"─── stderr ───");
                AppendOutput(_output, result.Stderr);
            }
            AppendOutput(_output, $"─── ExitCode: {result.ExitCode}  |  Süre: {result.Duration.TotalSeconds:F1}s ───");
        }
        catch (Exception ex)
        {
            AppendOutput(_output, $"❌ Hata: {ex.Message}");
        }
        finally
        {
            _btnRun.Enabled = true;
            _btnCancel.Enabled = false;
            _cts.Dispose();
            _cts = null;
        }
    }
}
