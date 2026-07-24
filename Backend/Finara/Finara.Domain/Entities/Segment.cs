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
    public Guid BankId { get; private set; }
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

    // RFM birincil/tek karar mekanizması olarak KALIYOR — bu sadece talep üzerine (kullanıcı
    // butona basınca) üretilen, insan-okunur bir yorum katmanı. null ise hiç üretilmemiş
    // demektir (segment yeniden hesaplanınca yeni bir Segment satırı oluştuğu için otomatik
    // olarak "sıfırlanmış" olur, ayrı bir invalidation mantığı gerekmiyor).
    public string? AiCommentary { get; private set; }

    private Segment() { }

    public static Segment Create(
        Guid bankId,
        Guid customerId,
        int recencyScore,
        int frequencyScore,
        int monetaryScore,
        SpendingPropensity propensity,
        TransactionCategory? dominantCategory = null)
    {
        if (bankId == Guid.Empty)
            throw new ArgumentException("Geçerli bir banka kimliği gerekli.", nameof(bankId));

        if (customerId == Guid.Empty)
            throw new ArgumentException("Geçerli bir müşteri kimliği gerekli.", nameof(customerId));

        ValidateScore(recencyScore, nameof(recencyScore));
        ValidateScore(frequencyScore, nameof(frequencyScore));
        ValidateScore(monetaryScore, nameof(monetaryScore));

        return new Segment
        {
            BankId = bankId,
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

    public void SetAiCommentary(string commentary)
    {
        if (string.IsNullOrWhiteSpace(commentary))
            throw new ArgumentException("AI yorumu boş olamaz.", nameof(commentary));

        AiCommentary = commentary.Trim();
        MarkUpdated();
    }

    private static void ValidateScore(int score, string paramName)
    {
        if (score is < 1 or > 5)
            throw new ArgumentOutOfRangeException(paramName, "RFM skoru 1 ile 5 arasında olmalı.");
    }
}