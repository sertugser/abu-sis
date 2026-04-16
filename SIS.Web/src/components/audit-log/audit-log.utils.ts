import type { AuditLogSummaryDto } from '../../api/auditLog.api';
import { ENTITY_TYPE_LABELS, ACTION_CONFIG } from './audit-log.constants';

export function formatRelativeTime(timestamp: string): string {
  const now = Date.now();
  const t = new Date(timestamp).getTime();
  const diffSec = Math.floor((now - t) / 1000);

  if (diffSec < 60) return 'Az önce';
  if (diffSec < 3600) return `${Math.floor(diffSec / 60)} dk önce`;
  if (diffSec < 86400) return `${Math.floor(diffSec / 3600)} saat önce`;
  if (diffSec < 604800) return `${Math.floor(diffSec / 86400)} gün önce`;

  return new Date(timestamp).toLocaleDateString('tr-TR', {
    day: '2-digit', month: 'short', year: 'numeric',
  });
}

export function formatFullDate(timestamp: string): string {
  return new Date(timestamp).toLocaleString('tr-TR', {
    day: '2-digit', month: 'long', year: 'numeric',
    hour: '2-digit', minute: '2-digit', second: '2-digit',
  });
}

export function formatJson(value: string): string {
  try { return JSON.stringify(JSON.parse(value), null, 2); }
  catch { return value; }
}

export function getErrorMessage(error: unknown, fallbackMessage: string): string {
  if (
    typeof error === 'object'
    && error !== null
    && 'response' in error
    && typeof (error as { response?: { data?: { error?: unknown } } }).response?.data?.error === 'string'
  ) {
    return (error as { response: { data: { error: string } } }).response.data.error;
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallbackMessage;
}

export function getUserInitials(name: string | undefined): string {
  if (!name) return '#';
  const parts = name.trim().split(' ');
  return parts.length >= 2
    ? (parts[0][0] + parts[parts.length - 1][0]).toUpperCase()
    : name.substring(0, 2).toUpperCase();
}

export function buildSummary(log: AuditLogSummaryDto): string {
  const user = log.userName ?? (log.userId != null ? `Kullanıcı #${log.userId}` : 'Sistem');
  const entity = ENTITY_TYPE_LABELS[log.entityType] ?? log.entityType;
  const actionCfg = ACTION_CONFIG[log.action];
  const action = actionCfg?.label ?? log.action;

  switch (log.action) {
    case 'Created':
      return `${user}, yeni bir ${entity} kaydı oluşturdu`;
    case 'Modified':
      return `${user}, bir ${entity} kaydını güncelledi`;
    case 'Deleted':
      return `${user}, bir ${entity} kaydını sildi`;
    case 'LoginFailed':
      return `${log.entityId ?? 'Bilinmeyen'} adresiyle başarısız giriş denemesi`;
    case 'LoginSuccess':
      return `${log.entityId ?? user} adresiyle başarılı giriş yapıldı`;
    case 'ScheduleAssigned':
      return `${user}, bir ders programı ataması yaptı`;
    case 'ScheduleUnassigned':
      return `${user}, bir ders programı atamasını kaldırdı`;
    case 'DataAccess':
      return `${user}, ${entity} verilerine erişim sağladı`;
    case 'SystemError':
      return `Sistem hatası oluştu`;
    case 'Unauthorized':
    case 'Forbidden':
      return `${user ? user + ' — ' : ''}yetkisiz erişim denemesi`;
    default:
      return `${user} — ${action} (${entity})`;
  }
}

export function generatePageNumbers(current: number, total: number): (number | '...')[] {
  if (total <= 7) return Array.from({ length: total }, (_, i) => i + 1);

  const pages: (number | '...')[] = [1];
  if (current > 3) pages.push('...');

  const start = Math.max(2, current - 1);
  const end = Math.min(total - 1, current + 1);
  for (let i = start; i <= end; i++) pages.push(i);

  if (current < total - 2) pages.push('...');
  pages.push(total);
  return pages;
}
