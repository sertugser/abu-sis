namespace SIS.Domain.Enums;

public enum StudentEntranceType
{
    Osym = 1,                 // ÖSYM / YKS yerleştirme
    Dgs = 2,                  // Dikey Geçiş Sınavı
    HorizontalTransfer = 3,   // Yatay Geçiş
    InternationalStudent = 4, // Yabancı Uyruklu (YÖS vb.)
    SpecialTalent = 5,        // Özel Yetenek
    Other = 99,
}
