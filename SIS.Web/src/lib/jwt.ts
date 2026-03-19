export interface JwtPayload {
  [key: string]: string | string[];
}

export function decodeJwt(token: string): JwtPayload {
  try {
    const base64Url = token.split('.')[1];
    const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split('')
        .map(c => '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2))
        .join('')
    );
    return JSON.parse(jsonPayload);
  } catch {
    return {};
  }
}

const CLAIM_NAME_ID = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier';
const CLAIM_EMAIL = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress';
const CLAIM_NAME = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';
const CLAIM_ROLE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';

export function extractUser(payload: JwtPayload) {
  const roles = Array.isArray(payload[CLAIM_ROLE])
    ? payload[CLAIM_ROLE]
    : payload[CLAIM_ROLE]
      ? [payload[CLAIM_ROLE] as string]
      : [];

  return {
    userId: parseInt(payload[CLAIM_NAME_ID] as string, 10) || 0,
    name: (payload[CLAIM_NAME] as string) || '',
    email: (payload[CLAIM_EMAIL] as string) || '',
    roles,
  };
}
