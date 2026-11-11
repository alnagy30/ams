using BH_AEP.Application.Common;
using QRCoder;

namespace BH_AEP.Infrastructure.Services;

public class QrService : IQrService
{
    public byte[] GeneratePng(string content)
    {
        using var generator = new QRCodeGenerator();
        var data = generator.CreateQrCode(content, QRCodeGenerator.ECCLevel.Q);
        using var qr = new PngByteQRCode(data);
        return qr.GetGraphic(8);
    }
}
