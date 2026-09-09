using System.Diagnostics;
using System.Reflection;
using System.Text;

namespace StudioOneTools.Avalonia.Help;

public static class HelpService
{
    // Resource name is tied to this assembly, not the linked StudioProToolsHelp.html's
    // original (WPF) assembly -- that's why this file isn't just a linked copy of
    // StudioOneTools.App/Help/HelpService.cs.
    private const string ResourceName = "StudioOneTools.Avalonia.Help.StudioProToolsHelp.html";

    public static void Open()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            using var stream = assembly.GetManifestResourceStream(ResourceName);

            if (stream is null)
            {
                return;
            }

            using var reader = new StreamReader(stream, Encoding.UTF8);
            var html         = reader.ReadToEnd();

            var tempPath = Path.Combine(Path.GetTempPath(), "StudioProToolsHelp.html");
            File.WriteAllText(tempPath, html, Encoding.UTF8);

            Process.Start(new ProcessStartInfo
            {
                FileName        = tempPath,
                UseShellExecute = true,
            });
        }
        catch
        {
            // Non-critical; silently ignore if the help file cannot be opened.
        }
    }
}
