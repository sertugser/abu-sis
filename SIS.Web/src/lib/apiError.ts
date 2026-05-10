export function getApiErrorMessage(error: unknown, fallback: string): string {
  if (
    typeof error === 'object' && error !== null && 'response' in error
    && (error as { response?: { status?: number } }).response?.status === 401
  ) {
    return 'Oturum süreniz doldu. Lütfen tekrar giriş yapın.';
  }

  if (
    typeof error === 'object' && error !== null && 'response' in error
    && (error as { response?: { status?: number } }).response?.status === 403
  ) {
    return 'Bu işlem için yetkiniz bulunmuyor.';
  }

  if (
    typeof error === 'object' && error !== null && 'response' in error
    && typeof (error as { response?: { data?: { error?: unknown; message?: unknown } } }).response?.data?.error === 'string'
  ) {
    return (error as { response: { data: { error: string } } }).response.data.error;
  }

  if (
    typeof error === 'object' && error !== null && 'response' in error
    && typeof (error as { response?: { data?: { error?: unknown; message?: unknown } } }).response?.data?.message === 'string'
  ) {
    return (error as { response: { data: { message: string } } }).response.data.message;
  }

  if (error instanceof Error && error.message) {
    return error.message;
  }

  return fallback;
}

export function toApiError(error: unknown, fallback: string) {
  return new Error(getApiErrorMessage(error, fallback));
}
