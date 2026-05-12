// Duyuru Kategorileri (Madde 3) — backend AnnouncementCategoryDto ile eşleşmeli

export interface AnnouncementCategory {
  id: number;
  name: string;
  colorHex: string;
  iconName: string;
  sortOrder: number;
  isSystem: boolean;
  createdAt: string;
  updatedAt: string | null;
}

export interface AnnouncementCategorySummary {
  id: number;
  name: string;
  colorHex: string;
  iconName: string;
}

export interface CreateAnnouncementCategoryPayload {
  name: string;
  colorHex: string;
  iconName: string;
  sortOrder: number;
}

export interface UpdateAnnouncementCategoryPayload {
  name: string;
  colorHex: string;
  iconName: string;
  sortOrder: number;
}
