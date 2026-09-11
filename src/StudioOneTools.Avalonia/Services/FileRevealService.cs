using System.Diagnostics;

namespace StudioOneTools.Avalonia.Services;

public static class FileRevealService
{
    public static void Reveal(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName        = "explorer.exe",
                Arguments       = $"\"{path}\"",
                UseShellExecute = true,
            });
        }
        else if (OperatingSystem.IsMacOS())
        {
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName        = "open",
                Arguments       = $"\"{path}\"",
                UseShellExecute = false,
            });
        }
        else
        {
            // Linux, best-effort.
            using var process = Process.Start(new ProcessStartInfo
            {
                FileName        = "xdg-open",
                Arguments       = $"\"{path}\"",
                UseShellExecute = false,
            });
        }
    }
}
