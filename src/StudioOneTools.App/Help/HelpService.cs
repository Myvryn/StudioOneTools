using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text;

namespace StudioOneTools.App.Help;

public static class HelpService
{
    private const string ResourceName = "StudioOneTools.App.Help.StudioProToolsHelp.html";

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
