namespace StudioOneTools.Avalonia.Services;

public static class WavDurationReader
{
    public static bool TryGetDuration(string filePath, out TimeSpan duration)
    {
        duration = TimeSpan.Zero;

        try
        {
            using var stream = File.OpenRead(filePath);
            using var reader = new BinaryReader(stream);

            if (stream.Length < 44 || new string(reader.ReadChars(4)) != "RIFF")
            {
                return false;
            }

            reader.ReadInt32(); // RIFF chunk size
            if (new string(reader.ReadChars(4)) != "WAVE")
            {
                return false;
            }

            ushort channels        = 0;
            uint   sampleRate      = 0;
            ushort bitsPerSample   = 0;
            uint   dataChunkLength = 0;
            var    haveFormat      = false;
            var    haveData        = false;

            while (stream.Position + 8 <= stream.Length && !(haveFormat && haveData))
            {
                var chunkId     = new string(reader.ReadChars(4));
                var chunkLength = reader.ReadUInt32();

                if (chunkId == "fmt ")
                {
                    var chunkStart = stream.Position;

                    reader.ReadInt16(); // audio format
                    channels      = reader.ReadUInt16();
                    sampleRate    = reader.ReadUInt32();
                    reader.ReadInt32();  // byte rate
                    reader.ReadInt16();  // block align
                    bitsPerSample = reader.ReadUInt16();

                    stream.Position = chunkStart + chunkLength + (chunkLength % 2);
                    haveFormat      = true;
                }
                else if (chunkId == "data")
                {
                    dataChunkLength = chunkLength;
                    haveData        = true;
                    stream.Position += chunkLength + (chunkLength % 2);
                }
                else
                {
                    stream.Position += chunkLength + (chunkLength % 2);
                }
            }

            if (!haveFormat || !haveData || channels == 0 || sampleRate == 0 || bitsPerSample == 0)
            {
                return false;
            }

            var bytesPerSecond = sampleRate * channels * (bitsPerSample / 8.0);

            if (bytesPerSecond <= 0)
            {
                return false;
            }

            duration = TimeSpan.FromSeconds(dataChunkLength / bytesPerSecond);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
