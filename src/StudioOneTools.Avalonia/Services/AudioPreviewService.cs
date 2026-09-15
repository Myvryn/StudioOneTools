using System.Diagnostics;
using Avalonia.Threading;

namespace StudioOneTools.Avalonia.Services;

/// <summary>
/// Cross-platform replacement for WPF's <c>System.Windows.Media.MediaPlayer</c>. Plays a WAV file
/// through a hidden, non-interactive native process (PowerShell's <c>SoundPlayer</c> on Windows,
/// <c>afplay</c> on macOS) -- no visible window, same as <see cref="FileRevealService"/>'s OS-branch
/// style. Position isn't reported by either process, so progress is simulated from elapsed wall-clock
/// time against the WAV file's own header-computed duration.
/// </summary>
public sealed class AudioPreviewService : IAudioPreviewService, IDisposable
{
    #region Fields

    private Process?   _process;
    private Stopwatch? _stopwatch;

    #endregion

    #region Properties

    public bool IsPlaying { get; private set; }

    public TimeSpan Duration { get; private set; }

    public TimeSpan Elapsed => _stopwatch is null
        ? TimeSpan.Zero
        : _stopwatch.Elapsed < Duration ? _stopwatch.Elapsed : Duration;

    #endregion

    #region Events

    public event EventHandler? Stopped;

    #endregion

    #region Public Methods

    public void Play(string filePath)
    {
        Stop();

        WavDurationReader.TryGetDuration(filePath, out var duration);
        Duration = duration;

        var startInfo = new ProcessStartInfo
        {
            UseShellExecute = false,
            CreateNoWindow  = true,
        };

        if (OperatingSystem.IsWindows())
        {
            startInfo.FileName = "powershell";
            startInfo.ArgumentList.Add("-NoProfile");
            startInfo.ArgumentList.Add("-NonInteractive");
            startInfo.ArgumentList.Add("-Command");
            startInfo.ArgumentList.Add($"(New-Object Media.SoundPlayer '{filePath.Replace("'", "''")}').PlaySync()");
        }
        else
        {
            startInfo.FileName = "afplay";
            startInfo.ArgumentList.Add(filePath);
        }

        var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
        process.Exited += OnProcessExited;

        _process   = process;
        _stopwatch = Stopwatch.StartNew();
        IsPlaying  = true;

        try
        {
            process.Start();
        }
        catch
        {
            process.Exited -= OnProcessExited;
            _process        = null;
            _stopwatch      = null;
            IsPlaying       = false;
            throw;
        }
    }

    public void Stop()
    {
        if (_process is { } process)
        {
            process.Exited -= OnProcessExited;

            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
            }
            catch
            {
                // Best-effort -- process may have already exited between the check and the kill.
            }

            process.Dispose();
            _process = null;
        }

        _stopwatch = null;
        IsPlaying  = false;
    }

    public void Dispose() => Stop();

    #endregion

    #region Private Methods

    private void OnProcessExited(object? sender, EventArgs e)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (!IsPlaying)
            {
                return;
            }

            _process?.Dispose();
            _process   = null;
            _stopwatch = null;
            IsPlaying  = false;

            Stopped?.Invoke(this, EventArgs.Empty);
        });
    }

    #endregion
}
