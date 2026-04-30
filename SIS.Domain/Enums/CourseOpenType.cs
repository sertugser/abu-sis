namespace SIS.Domain.Enums;

/// <summary>
/// "Açma Tipi" — how a course offering is opened. Defaults to Standart for new courses.
/// JsonStringEnumConverter serializes as string ("Standart", "Closed", "PendingApproval").
/// </summary>
public enum CourseOpenType
{
    Standart = 1,
    Closed = 2,             // "Kapatıldı"
    PendingApproval = 3,    // "Açık (Onay Bekleniyor)"
}
