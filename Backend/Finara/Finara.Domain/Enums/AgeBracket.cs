namespace Finara.Domain.Enums;

// Faz 8.3 — CohortEngine'in yaşı gruplamak için kullandığı sabit aralıklar.
// Customer.DateOfBirth 18 yaş altını zaten reddediyor, bu yüzden 18'in altı yok.
public enum AgeBracket
{
    Age18To25,
    Age26To35,
    Age36To45,
    Age46To55,
    Age56Plus
}
