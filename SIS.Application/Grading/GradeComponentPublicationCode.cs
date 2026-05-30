namespace SIS.Application.Grading;

/// <summary>
/// Komponent adından (Vize, Final, Bütünleme, Ödev, ...) öğrenciye-yayın için kullanılan
/// <c>IWebModuleControlService</c> kontrol kodunu çözer.
///
/// Tek-kaynak: bu mantık aynı anda hem öğrenci görünürlük hesabında
/// (StudentController.GetGradeStatistics) hem hoca-ilan endpoint'inde (GradeController.publish)
/// gerekli. İki yerde kopyalanırsa "ikibaşlılık" doğar — buradan okurlar.
///
/// Eşleşme önceliği:
///   1. mazeret / excuse → "midterm-excuse-grade-publication" (eğer ara/midterm/vize de geçiyorsa)
///                         veya "makeup-grade-publication" (diğer mazeret tipleri)
///   2. ara / midterm / vize → "midterm-grade-publication"
///   3. bütünleme / butunleme / makeup / yys → "makeup-grade-publication"
///   4. yarıyıl / yariyil / final → "final-grade-publication"
///   5. default → "letter-grade-publication"
/// </summary>
public static class GradeComponentPublicationCode
{
    public static string Resolve(string componentName)
    {
        var normalized = componentName.Trim().ToLowerInvariant();

        if (normalized.Contains("mazeret") || normalized.Contains("excuse"))
        {
            return normalized.Contains("ara") || normalized.Contains("midterm") || normalized.Contains("vize")
                ? "midterm-excuse-grade-publication"
                : "makeup-grade-publication";
        }

        if (normalized.Contains("ara") || normalized.Contains("midterm") || normalized.Contains("vize"))
            return "midterm-grade-publication";

        if (normalized.Contains("bütünleme") || normalized.Contains("butunleme") || normalized.Contains("makeup") || normalized.Contains("yys"))
            return "makeup-grade-publication";

        if (normalized.Contains("yarıyıl") || normalized.Contains("yariyil") || normalized.Contains("final"))
            return "final-grade-publication";

        return "letter-grade-publication";
    }
}
