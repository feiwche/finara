namespace Finara.Application.Contracts;

public record DailyOfferTrendPoint(DateTime Date, int Accepted, int Declined);

public record DailyCustomerGrowthPoint(DateTime Date, int NewCustomers);
