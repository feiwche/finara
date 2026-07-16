using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Finara.Domain.Common;
using Finara.Domain.Enums;

namespace Finara.Domain.Entities;

public class Campaign : BaseEntity
{
    public string Name { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public CampaignStatus Status { get; private set; } = CampaignStatus.Draft;

    // A/B test desteği: aynı kampanyanın farklı varyantları olabilir (örn. "A" mesajı, "B" mesajı)
    public string VariantId { get; private set; } = "default";

    public TransactionCategory? TargetCategory { get; private set; }
    public SpendingPropensity? TargetPropensity { get; private set; }

    // Next-Best-Offer motorunun "özel ürün uygunluğu" bonusu için — örn. seyahat sigortası
    // veya döviz avantajlı kart kampanyaları, yatırım hesabı/SWIFT kullanan müşterilere
    // ekstra puanla önerilsin diye. Faz 4.
    public bool TargetsInvestmentAccountHolders { get; private set; }
    public bool TargetsInternationalTransferUsers { get; private set; }

    public DateTime StartDateUtc { get; private set; }
    public DateTime? EndDateUtc { get; private set; }

    private readonly List<CampaignOffer> _offers = new();
    public IReadOnlyCollection<CampaignOffer> Offers => _offers.AsReadOnly();

    private Campaign() { }

    public static Campaign Create(
        string name,
        string description,
        DateTime startDateUtc,
        string variantId = "default",
        TransactionCategory? targetCategory = null,
        SpendingPropensity? targetPropensity = null,
        DateTime? endDateUtc = null,
        bool targetsInvestmentAccountHolders = false,
        bool targetsInternationalTransferUsers = false)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Kampanya adı boş olamaz.", nameof(name));

        if (endDateUtc.HasValue && endDateUtc.Value <= startDateUtc)
            throw new ArgumentException("Bitiş tarihi başlangıç tarihinden sonra olmalı.", nameof(endDateUtc));

        return new Campaign
        {
            Name = name.Trim(),
            Description = description?.Trim() ?? string.Empty,
            VariantId = variantId,
            TargetCategory = targetCategory,
            TargetPropensity = targetPropensity,
            StartDateUtc = startDateUtc,
            EndDateUtc = endDateUtc,
            TargetsInvestmentAccountHolders = targetsInvestmentAccountHolders,
            TargetsInternationalTransferUsers = targetsInternationalTransferUsers,
            Status = CampaignStatus.Draft
        };
    }

    public void Activate()
    {
        if (Status != CampaignStatus.Draft && Status != CampaignStatus.Paused)
            throw new InvalidOperationException($"'{Status}' durumundaki bir kampanya aktive edilemez.");

        if (EndDateUtc.HasValue && EndDateUtc.Value <= DateTime.UtcNow)
            throw new InvalidOperationException("Bitiş tarihi geçmişte kalan bir kampanya aktive edilemez.");

        Status = CampaignStatus.Active;
        MarkUpdated();
    }

    public void Pause()
    {
        if (Status != CampaignStatus.Active)
            throw new InvalidOperationException("Sadece aktif kampanyalar duraklatılabilir.");

        Status = CampaignStatus.Paused;
        MarkUpdated();
    }

    public void Complete()
    {
        if (Status != CampaignStatus.Active)
            throw new InvalidOperationException("Sadece aktif kampanyalar tamamlanabilir.");

        Status = CampaignStatus.Completed;
        MarkUpdated();
    }

    public void Archive()
    {
        Status = CampaignStatus.Archived;
        MarkUpdated();
    }
}