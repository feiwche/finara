using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Finara.Domain.Common;
using Finara.Domain.Enums;

namespace Finara.Domain.Entities;

public class Segment : BaseEntity
{
    public Guid CustomerId { get; private set; }
    public Customer? Customer { get; private set; }

    // RFM skorları, genelde 1-5 arası normalize edilir
    public int RecencyScore { get; private set; }
    public int FrequencyScore { get; private set; }
    public int MonetaryScore { get; private set; }

    public TransactionCategory? DominantCategory { get; private set; }
    public SpendingPropensity Propensity { get; private set; }
    public DateTime CalculatedAtUtc { get; private set; }

    // Bir müşterinin sadece EN GÜNCEL segment kaydı "aktif" sayılır, diğerleri geçmiş/arşiv.
    public bool IsActive { get; private set; }

    private Segment() { }

    public static Segment Create(
        Guid customerId,
        int recencyScore,
        int frequencyScore,
        int monetaryScore,
        SpendingPropensity propensity,
        TransactionCategory? dominantCategory = null)
    {
        if (customerId == Guid.Empty)
            throw new ArgumentException("Geçerli bir müşteri kimliği gerekli.", nameof(customerId));

        ValidateScore(recencyScore, nameof(recencyScore));
        ValidateScore(frequencyScore, nameof(frequencyScore));
        ValidateScore(monetaryScore, nameof(monetaryScore));

        return new Segment
        {
            CustomerId = customerId,
            RecencyScore = recencyScore,
            FrequencyScore = frequencyScore,
            MonetaryScore = monetaryScore,
            Propensity = propensity,
            DominantCategory = dominantCategory,
            CalculatedAtUtc = DateTime.UtcNow,
            IsActive = true
        };
    }

    public int GetTotalScore() => RecencyScore + FrequencyScore + MonetaryScore;

    public void Deactivate()
    {
        IsActive = false;
        MarkUpdated();
    }

    private static void ValidateScore(int score, string paramName)
    {
        if (score is < 1 or > 5)
            throw new ArgumentOutOfRangeException(paramName, "RFM skoru 1 ile 5 arasında olmalı.");
    }
}