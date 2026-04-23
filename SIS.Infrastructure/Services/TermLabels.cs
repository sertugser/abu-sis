using SIS.Domain.Enums;

namespace SIS.Infrastructure.Services;

internal static class TermLabels
{
    public static string Get(TermType t) => t switch
    {
        TermType.Fall         => "Güz",
        TermType.Spring       => "Bahar",
        TermType.Summer       => "Yaz",
        TermType.FallResit    => "Güz Tekders",
        TermType.SpringResit  => "Bahar Tekders",
        TermType.SummerResit  => "Yaz Tekders",
        TermType.SpringOther  => "Bahar Diğer",
        TermType.SummerOther  => "Yaz Diğer",
        TermType.Exemption    => "Muafiyet",
        _                     => t.ToString()
    };
}
