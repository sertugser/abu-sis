export function getErrorMessage(error: unknown, fallbackMessage: string): string {
  if (
    typeof error === 'object'
    && error !== null
    && 'response' in error
    && (error as { response?: { status?: number } }).response?.status === 401
  ) {
    return 'Oturum süreniz doldu. Lütfen tekrar giriş yapın.';
  }

  if (
    typeof error === 'object'
    && error !== null
    && 'response' in error
    && (error as { response?: { status?: number } }).response?.status === 403
  ) {
    return 'Bu işlem için yetkiniz bulunmuyor.';
  }

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
