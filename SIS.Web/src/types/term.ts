/**
 * Dropdown ve filter UI'ları için minimal term interface'i.
 *
 * Backend'in AdminTermDto'su daha fazla field içeriyor (year, status, vb.) —
 * bu type sadece "id + name" gerektiren UI bileşenleri için yapısal contract.
 * AdminTermDto bu interface'i structural olarak satisfy eder.
 */
export interface TermSummaryDto {
  id: number;
  name: string;
}
