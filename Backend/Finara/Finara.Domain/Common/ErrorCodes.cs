namespace Finara.Domain.Common;

// DomainException.Code için tek doğruluk kaynağı — frontend'deki errors.* çeviri anahtarları
// (public/i18n/{tr,en,de}.json, her iki app'te) bu sabitlerle BİREBİR aynı adları kullanmalı.
public static class ErrorCodes
{
    public const string AuthInvalidCredentials = "AUTH_INVALID_CREDENTIALS";
    public const string AuthRegistrationClosed = "AUTH_REGISTRATION_CLOSED";
    public const string AuthTwoFactorInvalidCode = "AUTH_TWOFACTOR_INVALID_CODE";
    public const string AuthRefreshTokenInvalid = "AUTH_REFRESH_TOKEN_INVALID";
    public const string AuthChallengeTokenInvalid = "AUTH_CHALLENGE_TOKEN_INVALID";
    public const string AuthResetLinkInvalid = "AUTH_RESET_LINK_INVALID";
    public const string AuthInvitationLinkInvalid = "AUTH_INVITATION_LINK_INVALID";
    public const string AuthDemoAccountRestricted = "AUTH_DEMO_ACCOUNT_RESTRICTED";

    public const string UserEmailAlreadyExists = "USER_EMAIL_ALREADY_EXISTS";
    public const string UserCannotDeactivateSelf = "USER_CANNOT_DEACTIVATE_SELF";
    public const string UserBankRequiredForRole = "USER_BANK_REQUIRED_FOR_ROLE";
    public const string UserInvitationAlreadyPending = "USER_INVITATION_ALREADY_PENDING";
    public const string UserTwoFactorAlreadyEnabled = "USER_TWOFACTOR_ALREADY_ENABLED";
    public const string UserTwoFactorAlreadyDisabled = "USER_TWOFACTOR_ALREADY_DISABLED";
    public const string UserInvitationAlreadyAccepted = "USER_INVITATION_ALREADY_ACCEPTED";
    public const string UserInvitationAlreadyRevoked = "USER_INVITATION_ALREADY_REVOKED";

    public const string BankCodeAlreadyExists = "BANK_CODE_ALREADY_EXISTS";
    public const string BankAlreadyInactive = "BANK_ALREADY_INACTIVE";
    public const string AuthzSuperAdminRequired = "AUTHZ_SUPERADMIN_REQUIRED";

    public const string CampaignCannotActivate = "CAMPAIGN_CANNOT_ACTIVATE";
    public const string CampaignExpired = "CAMPAIGN_EXPIRED";
    public const string CampaignCannotPause = "CAMPAIGN_CANNOT_PAUSE";
    public const string CampaignCannotComplete = "CAMPAIGN_CANNOT_COMPLETE";

    public const string OfferAlreadyResponded = "OFFER_ALREADY_RESPONDED";
    public const string OfferNoActiveSegment = "OFFER_NO_ACTIVE_SEGMENT";

    // Yerel dil modeli (Ollama) zamanında yanıt vermedi ya da hiç ayakta değil. Canlı
    // testte bulundu: Ollama'nın HttpClient'ında hiç timeout ayarlanmamıştı, .NET'in
    // varsayılan 100 saniyesine düşüyordu ve function-calling akışı CPU'da bundan uzun
    // sürdüğü için kullanıcı 100 saniye bekleyip genel bir 500 alıyordu.
    public const string AiServiceUnavailable = "AI_SERVICE_UNAVAILABLE";

    public const string CustomerEmailAlreadyExists = "CUSTOMER_EMAIL_ALREADY_EXISTS";
    public const string CustomerAlreadyErased = "CUSTOMER_ALREADY_ERASED";
    public const string CustomerCsvEmpty = "CUSTOMER_CSV_EMPTY";
    public const string CustomerCsvMissingColumns = "CUSTOMER_CSV_MISSING_COLUMNS";
    public const string CustomerCsvTooManyRows = "CUSTOMER_CSV_TOO_MANY_ROWS";
}
