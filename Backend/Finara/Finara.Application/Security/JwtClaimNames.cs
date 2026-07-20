namespace Finara.Application.Security;

// AuthService (JWT üretirken) ve CurrentUserContext (JWT'yi okurken) bu sabiti paylaşır.
public static class JwtClaimNames
{
    public const string BankId = "bank_id";
}
