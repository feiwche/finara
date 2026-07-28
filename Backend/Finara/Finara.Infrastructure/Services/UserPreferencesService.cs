using Finara.Application.Contracts;
using Finara.Application.Interfaces;
using Finara.Domain.Entities;
using Finara.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Finara.Infrastructure.Services;

public class UserPreferencesService(FinaraDbContext dbContext) : IUserPreferencesService
{
    public async Task<UserPreferencesDto?> GetPreferencesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        return user is null ? null : ToDto(user.Preferences);
    }

    public async Task<UserPreferencesDto?> UpdatePreferencesAsync(Guid userId, UpdateUserPreferencesRequest request, CancellationToken cancellationToken = default)
    {
        var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);
        if (user is null) return null;

        user.UpdatePreferences(new UserPreferences
        {
            Language = request.Language,
            ReceiveWeeklyDigestEmail = request.ReceiveWeeklyDigestEmail,
            DateFormat = request.DateFormat,
            TimeZoneId = request.TimeZoneId,
            TableDensity = request.TableDensity,
            DefaultPageSize = request.DefaultPageSize
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        return ToDto(user.Preferences);
    }

    private static UserPreferencesDto ToDto(UserPreferences p) =>
        new(p.Language, p.ReceiveWeeklyDigestEmail, p.DateFormat, p.TimeZoneId, p.TableDensity, p.DefaultPageSize);
}
