/**
 * Faz B B3c — DB-driven seçmeli ders tipi (TE/NDE/OC vb.) tipleri.
 * Backend kontrol: SIS.API/Controllers/ElectiveSubtypeController.cs
 * Karar #14: Code immutable; Name editable; flag'ler edit edilebilir ama UI uyarı verir.
 */
export interface ElectiveSubtypeDto {
  id: number;
  code: string;
  name: string;
  requiresFaculty: boolean;
  forbidsFaculty: boolean;
  requiresDepartment: boolean;
  forbidsDepartment: boolean;
  isActive: boolean;
  sortOrder: number;
}

export interface CreateElectiveSubtypeRequest {
  code: string;
  name: string;
  requiresFaculty?: boolean;
  forbidsFaculty?: boolean;
  requiresDepartment?: boolean;
  forbidsDepartment?: boolean;
  sortOrder?: number;
}

/**
 * Update DTO — Code YOK (immutable, Karar #14).
 * Tüm field'lar opsiyonel; backend partial update yapar.
 */
export interface UpdateElectiveSubtypeRequest {
  name?: string;
  requiresFaculty?: boolean;
  forbidsFaculty?: boolean;
  requiresDepartment?: boolean;
  forbidsDepartment?: boolean;
  isActive?: boolean;
  sortOrder?: number;
}
