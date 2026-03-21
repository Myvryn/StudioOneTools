using System.Net;
using System.Text;
using StudioOneTools.Core.Models;

namespace StudioOneTools.StudioOne.Services;

internal static class SongReadmeGenerator
{
    #region Public Methods

    public static string Generate(SongAnalysisResult analysis)
    {
        var generatedDate = DateTime.Now.ToString("MMMM d, yyyy 'at' h:mm tt");
        var sb            = new StringBuilder(8192);

        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang=\"en\">");
        sb.AppendLine("<head>");
        sb.AppendLine("  <meta charset=\"utf-8\" />");
        sb.AppendLine($"  <title>{H(analysis.SongName)} \u2014 Studio One Archive</title>");
        sb.AppendLine("  <style>");
        sb.AppendLine(Styles);
        sb.AppendLine("  </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("  <div class=\"container\">");

        // ── Header ──────────────────────────────────────────────────────────
        sb.AppendLine("    <header>");
        sb.AppendLine($"      <h1>{H(analysis.SongName)}</h1>");
        sb.AppendLine($"      <p class=\"subtitle\">Studio One Archive &nbsp;&middot;&nbsp; Generated {H(generatedDate)}</p>");
        sb.AppendLine("    </header>");

        // ── Overview ─────────────────────────────────────────────────────────
        sb.AppendLine("    <h2>Overview</h2>");
        sb.AppendLine("    <div class=\"stats-grid\">");
        sb.AppendLine($"      <div class=\"stat-card\"><div class=\"label\">Used WAV Files</div><div class=\"value\">{analysis.UsedWaveFiles.Count}</div></div>");
        sb.AppendLine($"      <div class=\"stat-card\"><div class=\"label\">Unused WAV Files</div><div class=\"value\">{analysis.UnusedWaveFiles.Count}</div></div>");
        sb.AppendLine($"      <div class=\"stat-card\"><div class=\"label\">Total Media Files</div><div class=\"value\">{analysis.MediaFiles.Count}</div></div>");
        sb.AppendLine($"      <div class=\"stat-card\"><div class=\"label\">Issues</div><div class=\"value\">{analysis.Issues.Count}</div></div>");
        sb.AppendLine("    </div>");
        sb.AppendLine("    <table class=\"info-table\">");
        sb.AppendLine($"      <tr><td>Song File</td><td>{H(analysis.SongFilePath)}</td></tr>");
        sb.AppendLine($"      <tr><td>Song Folder</td><td>{H(analysis.SongFolderPath)}</td></tr>");
        sb.AppendLine("    </table>");

        // ── Issues ────────────────────────────────────────────────────────────
        if (analysis.Issues.Count > 0)
        {
            sb.AppendLine("    <h2>Issues</h2>");
            sb.AppendLine("    <ul class=\"issue-list\">");

            foreach (var issue in analysis.Issues)
            {
                sb.AppendLine($"      <li>{H(issue)}</li>");
            }

            sb.AppendLine("    </ul>");
        }

        // ── All Plugins (Unique List) ──────────────────────────────────────────
        if (analysis.Plugins.Count > 0)
        {
            sb.AppendLine("    <h2>Plugins Used</h2>");
            sb.AppendLine("    <div class=\"plugin-summary\">");

            foreach (var pluginName in analysis.Plugins)
            {
                sb.AppendLine($"      <div class=\"plugin-badge\">{H(pluginName)}</div>");
            }

            sb.AppendLine("    </div>");
        }

        // ── Channels & Tracks ─────────────────────────────────────────────────
        sb.AppendLine("    <h2>Channels &amp; Tracks</h2>");

        if (analysis.Channels.Count > 0)
        {
            sb.AppendLine("    <div class=\"channel-grid\">");

            foreach (var channel in analysis.Channels)
            {
                sb.AppendLine("      <div class=\"channel-card\">");
                sb.AppendLine($"        <div class=\"channel-name\">{H(channel.Name)}</div>");

                if (channel.Plugins.Count > 0)
                {
                    sb.AppendLine("        <div class=\"channel-section\">");
                    sb.AppendLine("          <div class=\"section-label\">Plugins</div>");
                    sb.AppendLine("          <ul class=\"plugin-list\">");

                    foreach (var plugin in channel.Plugins)
                    {
                        sb.AppendLine($"            <li><strong>{H(plugin.DisplayName)}</strong></li>");
                    }

                    sb.AppendLine("          </ul>");
                    sb.AppendLine("        </div>");
                }

                if (channel.MediaFiles.Count > 0)
                {
                    sb.AppendLine("        <div class=\"channel-section\">");
                    sb.AppendLine("          <div class=\"section-label\">Audio Clips</div>");
                    sb.AppendLine("          <ul class=\"clip-list\">");

                    foreach (var mediaFile in channel.MediaFiles)
                    {
                        sb.AppendLine($"            <li>{H(mediaFile)}</li>");
                    }

                    sb.AppendLine("          </ul>");
                    sb.AppendLine("        </div>");
                }

                sb.AppendLine("      </div>");
            }

            sb.AppendLine("    </div>");
        }
        else
        {
            sb.AppendLine("    <p class=\"no-content\">No channel data detected.</p>");
        }

        sb.AppendLine($"    <footer>Generated by Studio One Tools &nbsp;&middot;&nbsp; {H(generatedDate)}</footer>");
        sb.AppendLine("  </div>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }

    #endregion

    #region Private Methods

    private static string H(string text) => WebUtility.HtmlEncode(text);

    #endregion

    #region Private Constants

    private const string Styles = """
        * { box-sizing: border-box; margin: 0; padding: 0; }
        body { font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; background: #f8f9fa; color: #333; padding: 40px 24px; }
        .container { max-width: 1100px; margin: 0 auto; }
        header { border-bottom: 3px solid #0078d4; padding-bottom: 16px; margin-bottom: 32px; }
        h1 { font-size: 2em; color: #1a1a2e; }
        .subtitle { color: #666; margin-top: 6px; font-size: 0.95em; }
        h2 { font-size: 0.9em; font-weight: 700; color: #0078d4; margin: 28px 0 12px; text-transform: uppercase; letter-spacing: 0.08em; }
        .stats-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(160px, 1fr)); gap: 12px; margin-bottom: 16px; }
        .stat-card { background: #fff; border: 1px solid #e0e0e0; border-radius: 8px; padding: 16px; }
        .stat-card .label { font-size: 0.75em; color: #888; text-transform: uppercase; letter-spacing: 0.05em; }
        .stat-card .value { font-size: 2em; font-weight: 700; color: #0078d4; margin-top: 4px; }
        .info-table { width: 100%; border-collapse: collapse; background: #fff; border: 1px solid #e0e0e0; margin-bottom: 8px; }
        .info-table td { padding: 10px 16px; border-bottom: 1px solid #f0f0f0; font-size: 0.9em; word-break: break-all; }
        .info-table td:first-child { font-weight: 600; color: #555; width: 140px; white-space: nowrap; word-break: normal; }
        .channel-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(280px, 1fr)); gap: 16px; margin-top: 8px; }
        .channel-card { background: #fff; border: 1px solid #e0e0e0; border-radius: 8px; padding: 16px; }
        .channel-name { font-size: 1em; font-weight: 700; color: #1a1a2e; margin-bottom: 12px; padding-bottom: 8px; border-bottom: 2px solid #f0f0f0; }
        .channel-section { margin-top: 12px; }
        .section-label { font-size: 0.7em; font-weight: 700; color: #888; text-transform: uppercase; letter-spacing: 0.08em; margin-bottom: 6px; }
        .plugin-list { list-style: none; display: flex; flex-wrap: wrap; gap: 6px; padding: 0; }
        .plugin-list li { background: #e8f0fe; color: #1a73e8; padding: 4px 12px; border-radius: 12px; font-size: 0.85em; font-weight: 500; }
        .plugin-list strong { font-weight: 600; }
        .plugin-summary { display: flex; flex-wrap: wrap; gap: 8px; margin-top: 8px; }
        .plugin-badge { background: #1a73e8; color: #fff; padding: 6px 14px; border-radius: 14px; font-size: 0.85em; font-weight: 500; }
        .clip-list { list-style: none; padding: 0; }
        .clip-list li { font-size: 0.85em; color: #555; padding: 3px 0; border-bottom: 1px solid #f8f8f8; }
        .no-content { color: #999; font-style: italic; font-size: 0.9em; }
        .issue-list { list-style: none; padding: 0; }
        .issue-list li { background: #fff3cd; border-left: 4px solid #ffc107; padding: 8px 14px; margin-bottom: 6px; font-size: 0.9em; }
        footer { margin-top: 48px; padding-top: 16px; border-top: 1px solid #e0e0e0; color: #999; font-size: 0.8em; text-align: center; }
        """;

    #endregion
}
