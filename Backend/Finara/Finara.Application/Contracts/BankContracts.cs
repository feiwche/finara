namespace Finara.Application.Contracts;

public record CreateBankRequest(string Name, string Code);

public record BankSummary(Guid Id, string Name, string Code, bool IsActive);
