using Finara.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Finara.Api.Realtime;

// Sadece sunucudan istemciye tek yönlü yayın için — istemcinin çağırabileceği hiçbir
// hub metodu yok, bağlanınca bank_id claim'ine göre bir gruba katılıyor
// (BankAdmin → "bank-{id}", SuperAdmin → "superadmins", CurrentUserContext'teki
// aynı ayrım). SignalRRealtimeNotifier bu gruplara yayın yapıyor.
[Authorize]
public class NotificationsHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        var bankId = Context.User?.FindFirst(JwtClaimNames.BankId)?.Value;
        var groupName = bankId is not null ? $"bank-{bankId}" : "superadmins";
        await Groups.AddToGroupAsync(Context.ConnectionId, groupName);
        await base.OnConnectedAsync();
    }
}
