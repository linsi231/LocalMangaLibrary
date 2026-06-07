using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LocalMangaLibrary;

public sealed class ImageCacheService
{
    private readonly StorageService _storage;

    public ImageCacheService(StorageService storage)
    {
        _storage = storage;
    }

    public string MakeThumbnail(string imagePath)
    {
        return MakeCachedImage(imagePath, _storage.ThumbDir, 360, 500, 82);
    }

    public string MakeReaderImage(string imagePath)
    {
        return MakeCachedImage(imagePath, _storage.ReaderDir, 1800, 2600, 88);
    }

    private static string MakeCachedImage(string imagePath, string cacheDir, int maxWidth, int maxHeight, int quality)
    {
        var file = new FileInfo(imagePath);
        if (!file.Exists)
        {
            return "";
        }

        Directory.CreateDirectory(cacheDir);
        var key = $"{file.FullName}|{file.LastWriteTimeUtc.Ticks}|{file.Length}|{maxWidth}|{maxHeight}";
        var name = Convert.ToHexString(SHA1.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant() + ".jpg";
        var target = Path.Combine(cacheDir, name);
        if (File.Exists(target))
        {
            return name;
        }

        try
        {
            using var stream = File.OpenRead(file.FullName);
            var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            var scale = Math.Min((double)maxWidth / frame.PixelWidth, (double)maxHeight / frame.PixelHeight);
            scale = Math.Min(1.0, scale);
            BitmapSource source = frame;
            if (scale < 1.0)
            {
                source = new TransformedBitmap(frame, new ScaleTransform(scale, scale));
                source.Freeze();
            }

            var encoder = new JpegBitmapEncoder { QualityLevel = quality };
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var output = File.Create(target);
            encoder.Save(output);
            return name;
        }
        catch
        {
            return "";
        }
    }
}
