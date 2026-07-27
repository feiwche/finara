namespace Finara.Infrastructure.Services;

// Tüm giden e-postaların paylaştığı marka çerçevesi — login sayfasındaki aynı koyu pine
// gradyanı (bkz. shared-ui auth sayfaları) üstte bir "wordmark" şeridi olarak kullanıyor.
// E-posta istemcileri (özellikle Outlook) CSS gradient'i desteklemediği için `bgcolor`
// niteliğiyle düz bir pine tonu (#175243) her zaman garanti fallback olarak veriliyor;
// gradyan sadece modern istemcilerde ek bir incelik.
internal static class EmailTemplates
{
    public static string Wrap(string bodyHtml) => $"""
        <!DOCTYPE html>
        <html lang="tr">
        <body style="margin:0;padding:0;background-color:#F5F3EC;font-family:'Segoe UI',Arial,sans-serif;">
          <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#F5F3EC;padding:32px 0;">
            <tr>
              <td align="center">
                <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color:#FFFFFF;border-radius:8px;overflow:hidden;border:1px solid #DCD6C7;">
                  <tr>
                    <td bgcolor="#175243" style="background-color:#175243;background-image:linear-gradient(135deg,#0D2A23,#175243,#227E67);padding:26px 32px;">
                      <span style="font-family:Georgia,'Times New Roman',serif;font-size:22px;font-weight:700;color:#F6F4EC;">Finara</span>
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:30px 32px;color:#17231E;font-size:15px;line-height:1.65;">
                      {bodyHtml}
                    </td>
                  </tr>
                  <tr>
                    <td style="padding:18px 32px;background-color:#F5F3EC;color:#4A5C53;font-size:12px;">
                      Bu e-posta Finara tarafından otomatik olarak gönderilmiştir.
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    public static string Button(string url, string label) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:22px 0;">
          <tr>
            <td bgcolor="#227E67" style="background-color:#227E67;border-radius:6px;">
              <a href="{url}" style="display:inline-block;padding:11px 22px;font-size:14px;font-weight:600;color:#FFFFFF;text-decoration:none;">{label}</a>
            </td>
          </tr>
        </table>
        <p style="font-size:12px;color:#4A5C53;word-break:break-all;">Bağlantı çalışmıyorsa: <a href="{url}" style="color:#227E67;">{url}</a></p>
        """;
}
