using Finara.Domain.Enums;

namespace Finara.Application.Contracts;

public record UserPreferencesDto(
    UiLanguage Language,
    bool ReceiveWeeklyDigestEmail,
    DateFormatPreference DateFormat,
    string TimeZoneId,
    TableDensity TableDensity,
    int DefaultPageSize);

public record UpdateUserPreferencesRequest(
    UiLanguage Language,
    bool ReceiveWeeklyDigestEmail,
    DateFormatPreference DateFormat,
    string TimeZoneId,
    TableDensity TableDensity,
    int DefaultPageSize);
