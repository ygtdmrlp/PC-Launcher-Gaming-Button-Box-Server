using System;
using QRCoder;

namespace PCLauncher.Core.Services;

public interface IQrCodeService
{
    byte[] GenerateQrCodePng(string url, int pixelsPerModule = 8);
    string GenerateQrCodeBase64(string url, int pixelsPerModule = 8);
}

public class QrCodeService : IQrCodeService
{
    public byte[] GenerateQrCodePng(string url, int pixelsPerModule = 8)
    {
        if (string.IsNullOrWhiteSpace(url))
            return Array.Empty<byte>();

        try
        {
            using var qrGenerator = new QRCodeGenerator();
            using var qrCodeData = qrGenerator.CreateQrCode(url, QRCodeGenerator.ECCLevel.M);
            var qrCode = new PngByteQRCode(qrCodeData);
            return qrCode.GetGraphic(pixelsPerModule);
        }
        catch
        {
            return Array.Empty<byte>();
        }
    }

    public string GenerateQrCodeBase64(string url, int pixelsPerModule = 8)
    {
        var bytes = GenerateQrCodePng(url, pixelsPerModule);
        if (bytes.Length == 0) return string.Empty;
        return $"data:image/png;base64,{Convert.ToBase64String(bytes)}";
    }
}
