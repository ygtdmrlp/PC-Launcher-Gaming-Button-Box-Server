using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace PCLauncher.Core.Services;

public interface IIconService
{
    string? ExtractIconAsBase64(string filePath);
    byte[]? GetIconBytes(string? iconBase64);
    string CreateDefaultIcon(string title, string category);
}

public class IconService : IIconService
{
    public string? ExtractIconAsBase64(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            using var icon = Icon.ExtractAssociatedIcon(filePath);
            if (icon == null) return null;

            using var bmp = icon.ToBitmap();
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            var bytes = ms.ToArray();
            return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
        }
        catch
        {
            return null;
        }
    }

    public byte[]? GetIconBytes(string? iconBase64)
    {
        if (string.IsNullOrWhiteSpace(iconBase64))
            return null;

        try
        {
            var raw = iconBase64;
            if (raw.Contains(','))
            {
                raw = raw.Substring(raw.IndexOf(',') + 1);
            }
            return Convert.FromBase64String(raw);
        }
        catch
        {
            return null;
        }
    }

    public string CreateDefaultIcon(string title, string category)
    {
        var letter = !string.IsNullOrWhiteSpace(title) 
            ? title.Trim().Substring(0, 1).ToUpperInvariant() 
            : "P";

        // Generate a clean 64x64 PNG with gradient/dark background and character
        try
        {
            using var bmp = new Bitmap(64, 64);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // Background rounded shape color based on category
                Color bgColor = category.ToLowerInvariant() switch
                {
                    "oyunlar" or "games" => Color.FromArgb(139, 92, 246),
                    "medya" or "media" => Color.FromArgb(236, 72, 153),
                    "araçlar" or "tools" => Color.FromArgb(59, 130, 246),
                    "çalışma" or "work" => Color.FromArgb(16, 185, 129),
                    _ => Color.FromArgb(99, 102, 241)
                };

                using var brush = new SolidBrush(bgColor);
                g.FillEllipse(brush, 2, 2, 60, 60);

                using var font = new Font("Segoe UI", 24, FontStyle.Bold, GraphicsUnit.Pixel);
                using var textBrush = new SolidBrush(Color.White);
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(letter, font, textBrush, new RectangleF(0, 0, 64, 64), sf);
            }

            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            return $"data:image/png;base64,{Convert.ToBase64String(ms.ToArray())}";
        }
        catch
        {
            return string.Empty;
        }
    }
}
