namespace SIS.Application.Common.DTOs;

/// <summary>
/// Standart hata yanıtı. Frontend <c>Code</c> alanına bakarak dile bağımlı string
/// matching yapmadan branch açabilir. <c>Error</c> insan-okunabilir mesaj.
/// </summary>
public sealed record ApiErrorResponse(string Error, string? Code = null);
