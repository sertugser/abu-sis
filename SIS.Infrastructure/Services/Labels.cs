using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

internal static class Labels
{
    public static string Osym(OsymRegistrationType t) => t switch
    {
        OsymRegistrationType.YksPlacement => "YKS Yerleştirme",
        OsymRegistrationType.HorizontalTransfer => "Yatay Geçiş",
        OsymRegistrationType.VerticalTransfer => "Dikey Geçiş",
        OsymRegistrationType.ForeignStudent => "Yabancı Uyruklu Öğrenci",
        _ => t.ToString()
    };

    public static string Quota(QuotaType t) => t switch
    {
        QuotaType.Yks => "YKS",
        QuotaType.LateralTransfer => "Yatay Geçiş",
        QuotaType.Postgraduate => "Lisansüstü",
        QuotaType.ForeignStudent => "Yabancı Uyruklu",
        _ => t.ToString()
    };
}
