using Finara.Application.Contracts;
using Finara.Domain.Enums;

namespace Finara.Infrastructure.Services;

// Tüm giden e-postaların paylaştığı marka çerçevesi — login sayfasındaki aynı koyu pine
// gradyanı (bkz. shared-ui auth sayfaları) üstte bir "wordmark" şeridi olarak kullanıyor.
// E-posta istemcileri (özellikle Outlook) CSS gradient'i desteklemediği için `bgcolor`
// niteliğiyle düz bir pine tonu (#175243) her zaman garanti fallback olarak veriliyor;
// gradyan sadece modern istemcilerde ek bir incelik.
//
// Dil desteği SADECE yönetici/personel e-postalarına (şifre sıfırlama, davet, haftalık
// özet — SmtpAdminEmailSender) uygulanıyor, alıcının User.Preferences.Language'ine göre.
// SmtpEmailDispatcher'ın kullandığı Wrap() çağrısı (bankanın kendi müşterisine giden
// bildirimler) bilinçli olarak dile dokunmuyor — `language` parametresi varsayılan
// Turkish'te kalıyor, bu tamamen ayrı bir kavram (bkz. IAdminEmailSender'daki not).
internal static class EmailTemplates
{
    public static string Wrap(string bodyHtml, UiLanguage language = UiLanguage.Turkish) => $"""
        <!DOCTYPE html>
        <html lang="{LangCode(language)}">
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
                      {FooterText(language)}
                    </td>
                  </tr>
                </table>
              </td>
            </tr>
          </table>
        </body>
        </html>
        """;

    public static string Button(string url, string label, UiLanguage language = UiLanguage.Turkish) => $"""
        <table role="presentation" cellpadding="0" cellspacing="0" style="margin:22px 0;">
          <tr>
            <td bgcolor="#227E67" style="background-color:#227E67;border-radius:6px;">
              <a href="{url}" style="display:inline-block;padding:11px 22px;font-size:14px;font-weight:600;color:#FFFFFF;text-decoration:none;">{label}</a>
            </td>
          </tr>
        </table>
        <p style="font-size:12px;color:#4A5C53;word-break:break-all;">{BrokenLinkText(language, url)}</p>
        """;

    private static string BrokenLinkText(UiLanguage language, string url) => language switch
    {
        UiLanguage.English => $"""If the button doesn't work: <a href="{url}" style="color:#227E67;">{url}</a>""",
        UiLanguage.German => $"""Falls die Schaltfläche nicht funktioniert: <a href="{url}" style="color:#227E67;">{url}</a>""",
        _ => $"""Bağlantı çalışmıyorsa: <a href="{url}" style="color:#227E67;">{url}</a>"""
    };

    private static string LangCode(UiLanguage language) => language switch
    {
        UiLanguage.English => "en",
        UiLanguage.German => "de",
        _ => "tr"
    };

    private static string FooterText(UiLanguage language) => language switch
    {
        UiLanguage.English => "This email was sent automatically by Finara.",
        UiLanguage.German => "Diese E-Mail wurde automatisch von Finara gesendet.",
        _ => "Bu e-posta Finara tarafından otomatik olarak gönderilmiştir."
    };

    public static string PasswordResetSubject(UiLanguage language) => language switch
    {
        UiLanguage.English => "Finara — Password Reset",
        UiLanguage.German => "Finara — Passwort zurücksetzen",
        _ => "Finara — Şifre Sıfırlama"
    };

    public static string PasswordResetPlainText(UiLanguage language, string resetUrl) => language switch
    {
        UiLanguage.English => $"Click the link to reset your password (valid for 30 minutes):\n\n{resetUrl}\n\nIf you did not request this, you can ignore this email.",
        UiLanguage.German => $"Klicken Sie auf den Link, um Ihr Passwort zurückzusetzen (30 Minuten gültig):\n\n{resetUrl}\n\nWenn Sie dies nicht angefordert haben, können Sie diese E-Mail ignorieren.",
        _ => $"Şifrenizi sıfırlamak için bağlantıya tıklayın (30 dakika geçerlidir):\n\n{resetUrl}\n\nBu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz."
    };

    public static string PasswordResetHtmlBody(UiLanguage language, string resetUrl) => language switch
    {
        UiLanguage.English => $"""
            <p>Hello,</p>
            <p>Click the button below to reset your password. The link is valid for <strong>30 minutes</strong>.</p>
            {Button(resetUrl, "Reset Password", language)}
            <p style="color:#4A5C53;font-size:13px;">If you did not request this, you can ignore this email.</p>
            """,
        UiLanguage.German => $"""
            <p>Hallo,</p>
            <p>Klicken Sie auf die Schaltfläche unten, um Ihr Passwort zurückzusetzen. Der Link ist <strong>30 Minuten</strong> gültig.</p>
            {Button(resetUrl, "Passwort zurücksetzen", language)}
            <p style="color:#4A5C53;font-size:13px;">Wenn Sie dies nicht angefordert haben, können Sie diese E-Mail ignorieren.</p>
            """,
        _ => $"""
            <p>Merhaba,</p>
            <p>Şifrenizi sıfırlamak için aşağıdaki bağlantıya tıklayın. Bağlantı <strong>30 dakika</strong> geçerlidir.</p>
            {Button(resetUrl, "Şifreyi Sıfırla", language)}
            <p style="color:#4A5C53;font-size:13px;">Bu isteği siz yapmadıysanız bu e-postayı yok sayabilirsiniz.</p>
            """
    };

    public static string InvitationSubject(UiLanguage language) => language switch
    {
        UiLanguage.English => "Finara — Account Invitation",
        UiLanguage.German => "Finara — Kontoeinladung",
        _ => "Finara — Hesap Daveti"
    };

    public static string InvitationPlainText(UiLanguage language, string acceptUrl) => language switch
    {
        UiLanguage.English => $"You've been invited to Finara. Click the link to create your account (valid for 24 hours):\n\n{acceptUrl}\n\nIf you did not request this invitation, you can ignore this email.",
        UiLanguage.German => $"Sie wurden zu Finara eingeladen. Klicken Sie auf den Link, um Ihr Konto zu erstellen (24 Stunden gültig):\n\n{acceptUrl}\n\nWenn Sie diese Einladung nicht angefordert haben, können Sie diese E-Mail ignorieren.",
        _ => $"Finara'ya davet edildiniz. Hesabınızı oluşturmak için bağlantıya tıklayın (24 saat geçerlidir):\n\n{acceptUrl}\n\nBu daveti siz talep etmediyseniz bu e-postayı yok sayabilirsiniz."
    };

    public static string InvitationHtmlBody(UiLanguage language, string acceptUrl) => language switch
    {
        UiLanguage.English => $"""
            <p>Hello,</p>
            <p>You've been invited to Finara. Click the button below to create your account. The link is valid for <strong>24 hours</strong>.</p>
            {Button(acceptUrl, "Accept Invitation", language)}
            <p style="color:#4A5C53;font-size:13px;">If you did not request this invitation, you can ignore this email.</p>
            """,
        UiLanguage.German => $"""
            <p>Hallo,</p>
            <p>Sie wurden zu Finara eingeladen. Klicken Sie auf die Schaltfläche unten, um Ihr Konto zu erstellen. Der Link ist <strong>24 Stunden</strong> gültig.</p>
            {Button(acceptUrl, "Einladung annehmen", language)}
            <p style="color:#4A5C53;font-size:13px;">Wenn Sie diese Einladung nicht angefordert haben, können Sie diese E-Mail ignorieren.</p>
            """,
        _ => $"""
            <p>Merhaba,</p>
            <p>Finara'ya davet edildiniz. Hesabınızı oluşturmak için aşağıdaki bağlantıya tıklayın. Bağlantı <strong>24 saat</strong> geçerlidir.</p>
            {Button(acceptUrl, "Daveti Kabul Et", language)}
            <p style="color:#4A5C53;font-size:13px;">Bu daveti siz talep etmediyseniz bu e-postayı yok sayabilirsiniz.</p>
            """
    };

    public static string AutomationAlertSubject(UiLanguage language) => language switch
    {
        UiLanguage.English => "Finara — Automation Alert",
        UiLanguage.German => "Finara — Automatisierungswarnung",
        _ => "Finara — Otomasyon Uyarısı"
    };

    // Gecikmiş bir işin "en son ne zaman başarılı olduğu" satırı — hiç çalışmamışsa
    // tarih yerine açık bir metin.
    private static string LastSuccessText(UiLanguage language, DateTime? lastSuccessUtc) => lastSuccessUtc is null
        ? language switch
        {
            UiLanguage.English => "has never completed successfully",
            UiLanguage.German => "wurde noch nie erfolgreich abgeschlossen",
            _ => "hiç başarıyla tamamlanmadı"
        }
        : language switch
        {
            UiLanguage.English => $"last success: {lastSuccessUtc:MM/dd/yyyy HH:mm} UTC",
            UiLanguage.German => $"letzter Erfolg: {lastSuccessUtc:dd.MM.yyyy HH:mm} UTC",
            _ => $"son başarılı tur: {lastSuccessUtc:dd.MM.yyyy HH:mm} UTC"
        };

    public static string AutomationAlertPlainText(UiLanguage language, IReadOnlyList<AutomationAlertJob> jobs)
    {
        var lines = string.Join("\n", jobs.Select(j => $"- {j.JobName}: {LastSuccessText(language, j.LastSuccessUtc)}"));
        return language switch
        {
            UiLanguage.English => $"""
                Hello,

                The following Finara background automations are overdue:

                {lines}

                This means segmentation and/or offer generation may have silently stopped. Please check the API logs and the /health/automation endpoint.
                """,
            UiLanguage.German => $"""
                Hallo,

                Die folgenden Finara-Hintergrundautomatisierungen sind überfällig:

                {lines}

                Das bedeutet, dass die Segmentierung und/oder Angebotserstellung möglicherweise stillschweigend gestoppt wurde. Bitte prüfen Sie die API-Logs und den Endpunkt /health/automation.
                """,
            _ => $"""
                Merhaba,

                Aşağıdaki Finara arka plan otomasyonları gecikmiş durumda:

                {lines}

                Bu, segmentasyon ve/veya teklif üretiminin sessizce durmuş olabileceği anlamına gelir. Lütfen API loglarını ve /health/automation ucunu kontrol edin.
                """
        };
    }

    public static string AutomationAlertHtmlBody(UiLanguage language, IReadOnlyList<AutomationAlertJob> jobs)
    {
        var (greeting, intro, footer) = language switch
        {
            UiLanguage.English => (
                "Hello",
                "The following Finara background automations are <strong>overdue</strong>:",
                "This means segmentation and/or offer generation may have silently stopped. Please check the API logs and the <code>/health/automation</code> endpoint."),
            UiLanguage.German => (
                "Hallo",
                "Die folgenden Finara-Hintergrundautomatisierungen sind <strong>überfällig</strong>:",
                "Das bedeutet, dass die Segmentierung und/oder Angebotserstellung möglicherweise stillschweigend gestoppt wurde. Bitte prüfen Sie die API-Logs und den Endpunkt <code>/health/automation</code>."),
            _ => (
                "Merhaba",
                "Aşağıdaki Finara arka plan otomasyonları <strong>gecikmiş</strong> durumda:",
                "Bu, segmentasyon ve/veya teklif üretiminin sessizce durmuş olabileceği anlamına gelir. Lütfen API loglarını ve <code>/health/automation</code> ucunu kontrol edin.")
        };

        var rowsHtml = string.Join("\n", jobs.Select(j =>
            $"""<tr><td style="padding:8px 0;border-bottom:1px solid #DCD6C7;font-weight:600;">{j.JobName}</td><td style="padding:8px 0;border-bottom:1px solid #DCD6C7;text-align:right;color:#4A5C53;">{LastSuccessText(language, j.LastSuccessUtc)}</td></tr>"""));

        return $"""
            <p>{greeting},</p>
            <p>{intro}</p>
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse;">
              {rowsHtml}
            </table>
            <p style="color:#4A5C53;font-size:13px;">{footer}</p>
            """;
    }

    public static string WeeklyDigestSubject(UiLanguage language, string bankName, DateTime weekStartUtc, DateTime weekEndUtc) => language switch
    {
        UiLanguage.English => $"Finara — {bankName} Weekly Summary ({weekStartUtc:MM/dd} - {weekEndUtc:MM/dd})",
        UiLanguage.German => $"Finara — {bankName} Wochenübersicht ({weekStartUtc:dd.MM} - {weekEndUtc:dd.MM})",
        _ => $"Finara — {bankName} Haftalık Özet ({weekStartUtc:dd.MM} - {weekEndUtc:dd.MM})"
    };

    public static string WeeklyDigestPlainText(UiLanguage language, string bankName, WeeklyDigestStats stats, DateTime weekStartUtc, DateTime weekEndUtc) => language switch
    {
        UiLanguage.English => $"""
            Hello,

            Summary for {bankName} for the week of {weekStartUtc:MM/dd/yyyy} - {weekEndUtc:MM/dd/yyyy}:

            - New customers: {stats.NewCustomers}
            - Accepted offers: {stats.OffersAccepted}
            - Declined offers: {stats.OffersDeclined}
            - Active campaigns: {stats.ActiveCampaigns}
            - Total customers: {stats.TotalCustomers}

            Visit the Overview screen in BackOffice for details.
            """,
        UiLanguage.German => $"""
            Hallo,

            Zusammenfassung für {bankName} für die Woche {weekStartUtc:dd.MM.yyyy} - {weekEndUtc:dd.MM.yyyy}:

            - Neue Kunden: {stats.NewCustomers}
            - Angenommene Angebote: {stats.OffersAccepted}
            - Abgelehnte Angebote: {stats.OffersDeclined}
            - Aktive Kampagnen: {stats.ActiveCampaigns}
            - Kunden insgesamt: {stats.TotalCustomers}

            Besuchen Sie den Überblick-Bildschirm in BackOffice für Details.
            """,
        _ => $"""
            Merhaba,

            {bankName} için {weekStartUtc:dd.MM.yyyy} - {weekEndUtc:dd.MM.yyyy} haftasının özeti:

            - Yeni müşteri: {stats.NewCustomers}
            - Kabul edilen teklif: {stats.OffersAccepted}
            - Reddedilen teklif: {stats.OffersDeclined}
            - Aktif kampanya: {stats.ActiveCampaigns}
            - Toplam müşteri: {stats.TotalCustomers}

            Detaylar için BackOffice panelindeki Genel Bakış ekranını ziyaret edin.
            """
    };

    public static string WeeklyDigestHtmlBody(UiLanguage language, string bankName, WeeklyDigestStats stats, DateTime weekStartUtc, DateTime weekEndUtc)
    {
        var (introTemplate, rows, footer) = language switch
        {
            UiLanguage.English => (
                $"<p><strong>{bankName}</strong> summary for the week of {weekStartUtc:MM/dd/yyyy} - {weekEndUtc:MM/dd/yyyy}:</p>",
                new[] { "New customers", "Accepted offers", "Declined offers", "Active campaigns", "Total customers" },
                "Visit the Overview screen in BackOffice for details."),
            UiLanguage.German => (
                $"<p><strong>{bankName}</strong> Zusammenfassung für die Woche {weekStartUtc:dd.MM.yyyy} - {weekEndUtc:dd.MM.yyyy}:</p>",
                new[] { "Neue Kunden", "Angenommene Angebote", "Abgelehnte Angebote", "Aktive Kampagnen", "Kunden insgesamt" },
                "Besuchen Sie den Überblick-Bildschirm in BackOffice für Details."),
            _ => (
                $"<p><strong>{bankName}</strong> için {weekStartUtc:dd.MM.yyyy} - {weekEndUtc:dd.MM.yyyy} haftasının özeti:</p>",
                new[] { "Yeni müşteri", "Kabul edilen teklif", "Reddedilen teklif", "Aktif kampanya", "Toplam müşteri" },
                "Detaylar için BackOffice panelindeki Genel Bakış ekranını ziyaret edin.")
        };

        var values = new[] { stats.NewCustomers, stats.OffersAccepted, stats.OffersDeclined, stats.ActiveCampaigns, stats.TotalCustomers };
        var rowsHtml = string.Join("\n", rows.Select((label, i) =>
        {
            var isLast = i == rows.Length - 1;
            var borderStyle = isLast ? "" : "border-bottom:1px solid #DCD6C7;";
            return $"""<tr><td style="padding:8px 0;{borderStyle}color:#4A5C53;">{label}</td><td style="padding:8px 0;{borderStyle}text-align:right;font-weight:600;">{values[i]}</td></tr>""";
        }));

        return $"""
            <p>{(language == UiLanguage.German ? "Hallo" : language == UiLanguage.English ? "Hello" : "Merhaba")},</p>
            {introTemplate}
            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:16px 0;border-collapse:collapse;">
              {rowsHtml}
            </table>
            <p style="color:#4A5C53;font-size:13px;">{footer}</p>
            """;
    }
}
