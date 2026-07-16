using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Finara.Domain.Common;

namespace Finara.Domain.Entities;

public enum OfferResponse
{
    Pending,
    Accepted,
    Declined,
    Expired
}

public class CampaignOffer : BaseEntity
{
    public Guid CampaignId { get; private set; }
    public Campaign? Campaign { get; private set; }

    public Guid CustomerId { get; private set; }
    public Customer? Customer { get; private set; }

    // Segmentasyon/NBO motorunun bu teklife verdiği güven skoru (0-100)
    public double ConfidenceScore { get; private set; }

    public OfferResponse Response { get; private set; } = OfferResponse.Pending;
    public DateTime PresentedAtUtc { get; private set; }
    public DateTime? RespondedAtUtc { get; private set; }

    private CampaignOffer() { }

    public static CampaignOffer Create(Guid campaignId, Guid customerId, double confidenceScore)
    {
        if (campaignId == Guid.Empty)
            throw new ArgumentException("Geçerli bir kampanya kimliği gerekli.", nameof(campaignId));

        if (customerId == Guid.Empty)
            throw new ArgumentException("Geçerli bir müşteri kimliği gerekli.", nameof(customerId));

        if (confidenceScore is < 0 or > 100)
            throw new ArgumentOutOfRangeException(nameof(confidenceScore), "Güven skoru 0 ile 100 arasında olmalı.");

        return new CampaignOffer
        {
            CampaignId = campaignId,
            CustomerId = customerId,
            ConfidenceScore = confidenceScore,
            PresentedAtUtc = DateTime.UtcNow,
            Response = OfferResponse.Pending
        };
    }

    public void Accept()
    {
        EnsurePending();
        Response = OfferResponse.Accepted;
        RespondedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Decline()
    {
        EnsurePending();
        Response = OfferResponse.Declined;
        RespondedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    public void Expire()
    {
        if (Response != OfferResponse.Pending) return;
        Response = OfferResponse.Expired;
        RespondedAtUtc = DateTime.UtcNow;
        MarkUpdated();
    }

    private void EnsurePending()
    {
        if (Response != OfferResponse.Pending)
            throw new InvalidOperationException($"Bu teklife zaten '{Response}' olarak yanıt verilmiş.");
    }
}
