using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace StudioOneTools.Core.Models;

public sealed class SongMediaFile : INotifyPropertyChanged
{
    private double _playbackProgress = 0.0;

    public required string FileName            { get; init; }

    public required string RelativePath        { get; init; }

    public required string FullPath            { get; init; }

    public string? SourceMediaId               { get; init; }

    public int UseCount                        { get; init; }

    public bool IsUsed                         { get; init; }

    public bool ExistsOnDisk                   { get; init; }

    public bool IsWaveFile                     { get; init; }

    public double PlaybackProgress
    {
        get => _playbackProgress;
        set
        {
            if (!_playbackProgress.Equals(value))
            {
                _playbackProgress = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
