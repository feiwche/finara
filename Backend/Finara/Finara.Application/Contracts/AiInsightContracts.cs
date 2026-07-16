namespace Finara.Application.Contracts;

public record AiInsightRequest(string? Question = null);

public record AiInsightResult(Guid CustomerId, string Question, string Answer, DateTime CreatedAtUtc);
