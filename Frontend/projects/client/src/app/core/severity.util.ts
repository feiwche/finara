export type TagSeverity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast';

// Üç ekranda (kampanya durumu, teklif durumu, müşteri eğilimi) aynı renk-kodlama
// mantığı tekrarlanıyordu — tek yerden yönetilsin diye buraya taşındı.
export function campaignStatusSeverity(status: string): TagSeverity {
  switch (status) {
    case 'Active':
      return 'success';
    case 'Draft':
      return 'info';
    case 'Paused':
      return 'warn';
    case 'Completed':
      return 'secondary';
    default:
      return 'danger';
  }
}

export function offerResponseSeverity(response: string): TagSeverity {
  switch (response) {
    case 'Accepted':
      return 'success';
    case 'Declined':
      return 'danger';
    case 'Expired':
      return 'secondary';
    default:
      return 'info';
  }
}

export function propensitySeverity(propensity: string): TagSeverity {
  switch (propensity) {
    case 'HighlyResponsive':
      return 'success';
    case 'NeedsEngagement':
      return 'warn';
    default:
      return 'info';
  }
}
