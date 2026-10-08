using SkiaSharp;

namespace Paintty.Imaging;

public interface IImageCodec
{
    /// <summary>Extensions <see cref="LoadAsync" /> understands, lower case with the dot.</summary>
    IReadOnlyList<string> ReadableExtensions { get; }

    /// <summary>Extensions <see cref="SaveAsync" /> can write, lower case with the dot.</summary>
    IReadOnlyList<string> WritableExtensions { get; }

    Task<SKBitmap> LoadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Encodes by the path's extension. The bitmap must not change until the task completes.</summary>
    Task SaveAsync(SKBitmap bitmap, string path, CancellationToken cancellationToken = default);
}

/// <summary>Loads anything SKCodec can decode and saves PNG, JPEG or WebP, chosen by extension.</summary>
public sealed class ImageCodec : IImageCodec
{
    private const int Quality = 92;

    private static readonly Dictionary<string, SKEncodedImageFormat> Encoders = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = SKEncodedImageFormat.Png,
        [".jpg"] = SKEncodedImageFormat.Jpeg,
        [".jpeg"] = SKEncodedImageFormat.Jpeg,
        [".webp"] = SKEncodedImageFormat.Webp
    };

    public IReadOnlyList<string> ReadableExtensions { get; } =
        [".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".ico", ".wbmp"];

    public IReadOnlyList<string> WritableExtensions { get; } = [.. Encoders.Keys];

    public Task<SKBitmap> LoadAsync(string path, CancellationToken cancellationToken = default) =>
        Task.Run(() =>
        {
            using var stream = File.OpenRead(path);
            using var codec = SKCodec.Create(stream)
                              ?? throw new InvalidDataException($"'{Path.GetFileName(path)}' is not an image Skia can read.");
            var decoded = new SKBitmap(Bitmaps.Info(codec.Info.Width, codec.Info.Height));
            var result = codec.GetPixels(decoded.Info, decoded.GetPixels());
            if (result is not (SKCodecResult.Success or SKCodecResult.IncompleteInput))
            {
                decoded.Dispose();
                throw new InvalidDataException($"Could not decode '{Path.GetFileName(path)}': {result}.");
            }

            decoded.NotifyPixelsChanged();
            return decoded;
        }, cancellationToken);

    public Task SaveAsync(SKBitmap bitmap, string path, CancellationToken cancellationToken = default)
    {
        if (!Encoders.TryGetValue(Path.GetExtension(path), out var format))
            throw new NotSupportedException(
                $"Can't save '{Path.GetExtension(path)}' files; use one of {string.Join(", ", WritableExtensions)}.");

        return Task.Run(() =>
        {
            using var image = SKImage.FromBitmap(bitmap);
            // Skia writes WebP losslessly only at quality 100, and an editor shouldn't lose pixels.
            using var data = image.Encode(format, format == SKEncodedImageFormat.Webp ? 100 : Quality)
                             ?? throw new InvalidOperationException($"Skia could not encode {format}.");

            // Write beside the target and swap it in, so a failed save never truncates the original.
            string temp = path + ".tmp";
            using (var stream = File.Create(temp))
                data.SaveTo(stream);
            File.Move(temp, path, overwrite: true);
        }, cancellationToken);
    }
}
