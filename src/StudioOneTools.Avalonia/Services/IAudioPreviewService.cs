namespace StudioOneTools.Avalonia.Services;

public interface IAudioPreviewService
{
    bool IsPlaying { get; }

    TimeSpan Duration { get; }

    TimeSpan Elapsed { get; }

    /// <summary>Raised when playback stops, whether it finished naturally or <see cref="Stop"/> was called.</summary>
    event EventHandler? Stopped;

    void Play(string filePath);

    void Stop();
}
