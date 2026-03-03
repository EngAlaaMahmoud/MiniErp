using System.Text;
using Net.Codecrete.QrCodeGenerator;

namespace MiniErp.Api.Infrastructure.Qr;

public static class QrCodeEngine
{
    public static string EncodeSvgBase64(string text)
    {
        var qr = QrCode.EncodeText(text, QrCode.Ecc.Medium);
        var svg = qr.ToSvgString(4);
        var bytes = Encoding.UTF8.GetBytes(svg);
        return Convert.ToBase64String(bytes);
    }
}

