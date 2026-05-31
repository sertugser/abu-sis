import type { ValidationError } from '../types/registration';

/**
 * Backend hata yanıtı yapısı. Üç tip body bekleniyor:
 *
 * 1) ApiErrorResponse — basit hata (`{ error, code? }`)
 * 2) RegistrationResponseDto — kayıt akışı validation hataları
 *    (`{ success, validationResult: { errors, warnings } }`)
 * 3) Eski stil (`{ message }`) — geriye uyumluluk için fallback
 */
interface RawErrorBody {
  error?: unknown;
  code?: unknown;
  message?: unknown;
  validationResult?: {
    errors?: ValidationError[];
    warnings?: ValidationError[];
    isValid?: boolean;
  };
}

/**
 * Frontend tarafında yakalanan API hatası.
 *
 * - `code`: backend tarafından üretilen stabil tanımlayıcı (örn. "ActiveTermNotFound").
 *   Türkçe metne göre branch açmak yerine bu alana bakılmalı.
 * - `validationErrors` / `validationWarnings`: kayıt akışındaki çoklu doğrulama hataları.
 *   Backend `BadRequest(result)` döndürdüğünde axios error throw eder; bu liste o body'den
 *   çıkarılır ve UI'a yansıtılır.
 * - `status`: HTTP durum kodu.
 */
export class ApiError extends Error {
  readonly code?: string;
  readonly status?: number;
  readonly validationErrors: ValidationError[];
  readonly validationWarnings: ValidationError[];

  constructor(params: {
    message: string;
    code?: string;
    status?: number;
    validationErrors?: ValidationError[];
    validationWarnings?: ValidationError[];
  }) {
    super(params.message);
    this.name = 'ApiError';
    this.code = params.code;
    this.status = params.status;
    this.validationErrors = params.validationErrors ?? [];
    this.validationWarnings = params.validationWarnings ?? [];
  }
}

function extractBody(error: unknown): { body: RawErrorBody | null; status: number | undefined } {
  if (typeof error !== 'object' || error === null || !('response' in error)) {
    return { body: null, status: undefined };
  }
  const response = (error as { response?: { status?: number; data?: unknown } }).response;
  const status = response?.status;
  const data = response?.data;
  if (typeof data !== 'object' || data === null) {
    return { body: null, status };
  }
  return { body: data as RawErrorBody, status };
}

export function toApiError(error: unknown, fallback: string): ApiError {
  const { body, status } = extractBody(error);

  // 401 / 403 — özel mesajlar
  if (status === 401) {
    return new ApiError({
      message: 'Oturum süreniz doldu. Lütfen tekrar giriş yapın.',
      code: 'Unauthorized',
      status,
    });
  }
  if (status === 403) {
    return new ApiError({
      message: 'Bu işlem için yetkiniz bulunmuyor.',
      code: 'Forbidden',
      status,
    });
  }

  // Validation result (RegistrationResponseDto.validationResult)
  if (body?.validationResult?.errors && body.validationResult.errors.length > 0) {
    const firstError = body.validationResult.errors[0];
    return new ApiError({
      message: firstError.message,
      code: firstError.rule,
      status,
      validationErrors: body.validationResult.errors,
      validationWarnings: body.validationResult.warnings ?? [],
    });
  }

  // ApiErrorResponse (`{ error: string, code?: string }`)
  if (body && typeof body.error === 'string') {
    return new ApiError({
      message: body.error,
      code: typeof body.code === 'string' ? body.code : undefined,
      status,
    });
  }

  // Eski stil (`{ message: string }`)
  if (body && typeof body.message === 'string') {
    return new ApiError({
      message: body.message,
      status,
    });
  }

  // Son çare: axios'un kendi mesajı
  if (error instanceof Error && error.message) {
    return new ApiError({ message: error.message, status });
  }

  return new ApiError({ message: fallback, status });
}

/**
 * Geriye uyumluluk için bırakıldı; yeni kod `toApiError` kullansın.
 * @deprecated Use `toApiError` and read `ApiError.message` instead.
 */
export function getApiErrorMessage(error: unknown, fallback: string): string {
  return toApiError(error, fallback).message;
}

/**
 * Basit hata mesajı çıkarıcı: `response.data.error` varsa döner, yoksa fallback.
 * GradeEntryPage / ScorePasteDialog / ExcelImportDialog tarafından ortak olarak kullanılır.
 */
export function getErrorMessage(error: unknown, fallback: string): string {
  if (error && typeof error === 'object' && 'response' in error) {
    const data = (error as { response?: { data?: { error?: string } } }).response?.data;
    if (data?.error) return data.error;
  }
  return fallback;
}
