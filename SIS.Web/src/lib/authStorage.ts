export const AUTH_STORAGE_KEYS = {
  token: 'sis_token',
  expiresAt: 'sis_token_expires',
  roles: 'sis_roles',
} as const;

export const AUTH_STORAGE_CHANGED_EVENT = 'sis:auth-storage-changed';

export type AuthStorageChangeReason = 'login' | 'logout' | 'expired' | 'invalid' | 'unauthorized';

export interface StoredAuthSession {
  token: string;
  expiresAt: string;
  roles: string[];
}

type AuthSessionReadResult =
  | { status: 'valid'; session: StoredAuthSession }
  | { status: 'missing' | 'expired' | 'invalid'; session: null };

interface ReadStoredAuthOptions {
  clearInvalid?: boolean;
  notify?: boolean;
}

interface ClearStoredAuthOptions {
  reason?: AuthStorageChangeReason;
  notify?: boolean;
}

function getStorage() {
  if (typeof window === 'undefined') return null;

  try {
    return window.localStorage;
  } catch {
    return null;
  }
}

function notifyAuthStorageChanged(reason: AuthStorageChangeReason) {
  if (typeof window === 'undefined') return;

  window.dispatchEvent(new CustomEvent(AUTH_STORAGE_CHANGED_EVENT, { detail: { reason } }));
}

export function subscribeToAuthStorageChanges(
  listener: (reason: AuthStorageChangeReason) => void,
) {
  if (typeof window === 'undefined') return () => undefined;

  const handler = (event: Event) => {
    const reason = event instanceof CustomEvent
      ? event.detail?.reason as AuthStorageChangeReason | undefined
      : undefined;
    listener(reason ?? 'invalid');
  };

  window.addEventListener(AUTH_STORAGE_CHANGED_EVENT, handler);
  return () => window.removeEventListener(AUTH_STORAGE_CHANGED_EVENT, handler);
}

export function clearStoredAuth(options: ClearStoredAuthOptions = {}) {
  const storage = getStorage();
  if (!storage) return;

  const hadStoredAuth = Object.values(AUTH_STORAGE_KEYS).some(key => storage.getItem(key) !== null);
  Object.values(AUTH_STORAGE_KEYS).forEach(key => storage.removeItem(key));

  if (options.notify !== false && hadStoredAuth) {
    notifyAuthStorageChanged(options.reason ?? 'logout');
  }
}

export function saveStoredAuth(session: StoredAuthSession) {
  const storage = getStorage();
  if (!storage) return;

  storage.setItem(AUTH_STORAGE_KEYS.token, session.token);
  storage.setItem(AUTH_STORAGE_KEYS.expiresAt, session.expiresAt);
  storage.setItem(AUTH_STORAGE_KEYS.roles, JSON.stringify(session.roles));
  notifyAuthStorageChanged('login');
}

export function readStoredAuth(options: ReadStoredAuthOptions = {}): AuthSessionReadResult {
  const storage = getStorage();
  if (!storage) return { status: 'missing', session: null };

  const token = storage.getItem(AUTH_STORAGE_KEYS.token);
  const expiresAt = storage.getItem(AUTH_STORAGE_KEYS.expiresAt);
  const rawRoles = storage.getItem(AUTH_STORAGE_KEYS.roles);
  const hasAnyAuthValue = token !== null || expiresAt !== null || rawRoles !== null;

  if (!hasAnyAuthValue) {
    return { status: 'missing', session: null };
  }

  if (!token || !expiresAt || !hasJwtShape(token)) {
    clearInvalidSession(options, 'invalid');
    return { status: 'invalid', session: null };
  }

  const expiry = Date.parse(expiresAt);
  if (Number.isNaN(expiry)) {
    clearInvalidSession(options, 'invalid');
    return { status: 'invalid', session: null };
  }

  if (expiry <= Date.now()) {
    clearInvalidSession(options, 'expired');
    return { status: 'expired', session: null };
  }

  return {
    status: 'valid',
    session: {
      token,
      expiresAt,
      roles: parseRoles(rawRoles),
    },
  };
}

export function getValidAccessToken(options: ReadStoredAuthOptions = {}) {
  const result = readStoredAuth(options);
  return result.status === 'valid' ? result.session.token : null;
}

export function hasValidStoredAuth() {
  return readStoredAuth().status === 'valid';
}

function clearInvalidSession(options: ReadStoredAuthOptions, reason: AuthStorageChangeReason) {
  if (!options.clearInvalid) return;

  clearStoredAuth({ reason, notify: options.notify });
}

function hasJwtShape(token: string) {
  const parts = token.split('.');
  return parts.length === 3 && parts.every(part => part.length > 0);
}

function parseRoles(rawRoles: string | null) {
  if (!rawRoles) return [];

  try {
    const parsed = JSON.parse(rawRoles) as unknown;
    return Array.isArray(parsed)
      ? parsed.filter((role): role is string => typeof role === 'string' && role.trim().length > 0)
      : [];
  } catch {
    return [];
  }
}
