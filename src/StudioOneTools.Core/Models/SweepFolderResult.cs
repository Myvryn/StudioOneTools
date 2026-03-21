namespace StudioOneTools.Core.Models;

public sealed class SweepFolderResult
{
    public required string FolderPath { get; init; }

    public required string FolderName { get; init; }

    public required string Reason     { get; init; }
}
