using Finara.Api.RateLimiting;
using Finara.Application.Contracts;
using Finara.Application.Interfaces;

namespace Finara.Api.Endpoints;

public static class BankEndpoints
{
    public static void MapBankEndpoints(this WebApplication app)
    {
        var banks = app.MapGroup("/api/banks").WithTags("Banks")
            .RequireRateLimiting(RateLimitPolicies.Global)
            .RequireAuthorization();

        banks.MapGet("/", async (IBankService service, CancellationToken ct) =>
            Results.Ok(await service.GetBanksAsync(ct)))
            .Produces<IReadOnlyList<BankSummary>>();

        // "/mine" — GetBanksAsync'in aksine SuperAdmin kısıtlaması yok, sade
        // RequireAuthorization() yeterli (BankAdmin/Analyst kendi bankasını görebilir).
        // Route sırası önemli: "/mine" içeren istek "/{id:guid}" ile karışmasın diye
        // spesifik segment önce tanımlanmalı — ama burada zaten farklı bir HTTP metodu/
        // segment olduğu için (banks/mine vs banks/{id}/deactivate) çakışma yok.
        banks.MapGet("/mine", async (IBankService service, CancellationToken ct) =>
        {
            var summary = await service.GetMyBankAsync(ct);
            return summary is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Banka bulunamadı",
                    detail: "Hesabınıza bağlı bir banka bulunamadı.")
                : Results.Ok(summary);
        })
        .Produces<BankSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        banks.MapPost("/", async (CreateBankRequest request, IBankService service, CancellationToken ct) =>
        {
            var summary = await service.CreateBankAsync(request, ct);
            return Results.Created($"/api/banks/{summary.Id}", summary);
        })
        .Produces<BankSummary>(StatusCodes.Status201Created)
        .RequireAuthorization("WriteAccess");

        banks.MapPost("/{id:guid}/deactivate", async (Guid id, IBankService service, CancellationToken ct) =>
        {
            var summary = await service.DeactivateBankAsync(id, ct);
            return summary is null
                ? Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "Banka bulunamadı",
                    detail: $"'{id}' kimlikli banka bulunamadı.")
                : Results.Ok(summary);
        })
        .Produces<BankSummary>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .RequireAuthorization("WriteAccess");
    }
}
