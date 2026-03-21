using StudioOneTools.StudioOne.Services;

namespace StudioOneTools.Tests;

public sealed class StudioOneSongAnalyzerTests
{
    [Fact]
    public void Analyze_ShouldIdentifyUsedAndUnusedWaveFiles()
    {
        using var project = TestSongProject.Create();

        project.WriteMediaFile(@"Media\used.wav", "used");
        project.WriteMediaFile(@"Media\unused.wav", "unused");
        project.WriteMediaFile(@"Media\keep.txt", "keep");
        project.WriteSongArchive(
            usedAudioClipIds: ["{USED-CLIP}"],
            mediaPoolEntries:
            [
                TestSongProject.MediaPoolEntry.Create("{USED-CLIP}",  @"Media\used.wav",   1),
                TestSongProject.MediaPoolEntry.Create("{UNUSED-CLIP}",@"Media\unused.wav", 0),
            ]);

        var analyzer = new StudioOneSongAnalyzer();
        var result   = analyzer.Analyze(project.ProjectFolderPath);

        Assert.Single(result.UsedWaveFiles);
        Assert.Single(result.UnusedWaveFiles);
        Assert.Contains(result.MediaFiles, file => file.RelativePath == @"used.wav"   && file.IsUsed);
        Assert.Contains(result.MediaFiles, file => file.RelativePath == @"unused.wav" && !file.IsUsed);
        Assert.Contains(result.MediaFiles, file => file.RelativePath == @"keep.txt"   && !file.IsWaveFile);
        Assert.Empty(result.Issues);
    }

    [Fact]
    public void Analyze_ShouldReportMissingReferencedWaveFiles()
    {
        using var project = TestSongProject.Create();

        project.WriteMediaFile(@"Media\unused.wav", "unused");
        project.WriteSongArchive(
            usedAudioClipIds: ["{USED-CLIP}"],
            mediaPoolEntries:
            [
                TestSongProject.MediaPoolEntry.Create("{USED-CLIP}", @"Media\missing.wav", 1),
            ]);

        var analyzer = new StudioOneSongAnalyzer();
        var result   = analyzer.Analyze(project.ProjectFolderPath);

        Assert.True(result.HasMissingReferencedFiles);
        Assert.Single(result.MissingReferencedFiles);
        Assert.Contains("missing.wav", result.Issues.Single(), StringComparison.OrdinalIgnoreCase);
    }
}
