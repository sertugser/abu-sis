export interface AnnouncementReader {
  userId: number;
  displayName: string;
  email: string | null;
  readAt: string;
  acknowledgedAt: string | null;
}

export interface AnnouncementReadsByDay {
  day: string; // ISO date: yyyy-MM-dd
  count: number;
}

export interface AnnouncementStats {
  announcementId: number;
  readCount: number;
  acknowledgedCount: number;
  attachmentCount: number;
  firstReadAt: string | null;
  lastReadAt: string | null;
  recentReaders: AnnouncementReader[];
  dailyReads: AnnouncementReadsByDay[];
  readsInLast24h: number;
}
