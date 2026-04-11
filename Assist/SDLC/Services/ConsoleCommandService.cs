using System.Diagnostics;

namespace Assist.SDLC.Services;

using Assist.SDLC.Abstractions;
using Assist.SDLC.Domain;

/// <summary>
/// Runs console processes (dotnet build, test, npm, docker, etc.) asynchronously
/// and captures stdout / stderr.
/// </summary>
internal sealed class ConsoleCommandService : IConsoleCommandService
{
    public event EventHandler<string>? OutputReceived;

    public async Task<ConsoleCommandResult> RunAsync(ConsoleCommandRequest request, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var result = new ConsoleCommandResult { Command = request.Command };

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (request.Timeout > TimeSpan.Zero)
            cts.CancelAfter(request.Timeout);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = GetShell(),
                Arguments = GetShellArgs(request.Command),
                WorkingDirectory = request.WorkingDirectory ?? Environment.CurrentDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var proc = new Process { StartInfo = psi };
            proc.Start();

            // Read stdout / stderr in parallel
            var stdoutTask = proc.StandardOutput.ReadToEndAsync(cts.Token);
            var stderrTask = proc.StandardError.ReadToEndAsync(cts.Token);

            await proc.WaitForExitAsync(cts.Token);

            result.Stdout = await stdoutTask;
            result.Stderr = await stderrTask;
            result.ExitCode = proc.ExitCode;

            OutputReceived?.Invoke(this, result.Stdout);
        }
        catch (OperationCanceledException)
        {
            result.TimedOut = true;
            result.Cancelled = ct.IsCancellationRequested;
        }
        catch (Exception ex)
        {
            result.Stderr = ex.Message;
            result.ExitCode = -1;
        }

        sw.Stop();
        result.Duration = sw.Elapsed;
        return result;
    }

    private static string GetShell() =>
        OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash";

    private static string GetShellArgs(string command) =>
        OperatingSystem.IsWindows() ? $"/c {command}" : $"-c \"{command}\"";
}
