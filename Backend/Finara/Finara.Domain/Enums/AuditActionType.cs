using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Finara.Domain.Enums;

public enum AuditActionType
{
    SegmentAssigned,
    CampaignOfferPresented,
    CampaignOfferAccepted,
    CampaignOfferDeclined,
    CustomerCreated,
    TransactionRecorded,
    OfferDistributed,
    ConsentGranted,
    ConsentRevoked,
    // Sona eklendi (mevcut kayıtlı int değerlerin anlamını kaydırmamak için) —
    // yönetici hesabı işlemleri hiçbir müşteriyle ilgili değil, bkz. AuditLog.CustomerId.
    AdminCreated,
    AdminDeactivated,
    CustomerBulkImported,
    CustomerErased
}
