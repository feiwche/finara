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
    CustomerErased,
    // Davetle-kayıt akışı (register'ın auth'suz halinin kapatılmasıyla eklendi).
    AdminInvited,
    AdminInvitationAccepted,
    AdminInvitationRevoked,
    // Bir yöneticinin başka bir kullanıcının oturumlarını uzaktan sonlandırması —
    // hesabı tamamen pasifleştirmekten (AdminDeactivated) daha ölçülü bir ara adım
    // (ör. şüpheli bir oturum fark edildiğinde, hesabı kapatmadan önce).
    AdminSessionsRevoked
}
