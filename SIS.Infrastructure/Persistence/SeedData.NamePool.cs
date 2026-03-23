namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    private static readonly string[] MaleFirstNames =
    [
        "Ahmet", "Mehmet", "Mustafa", "Ali", "Hüseyin", "Hasan", "İbrahim", "İsmail",
        "Yusuf", "Osman", "Murat", "Ömer", "Ramazan", "Halil", "Süleyman", "Abdullah",
        "Recep", "Fatih", "Emre", "Burak", "Cem", "Serkan", "Onur", "Kadir",
        "Enes", "Furkan", "Berkay", "Kaan", "Baran", "Ege", "Arda", "Deniz",
        "Oğuzhan", "Tolga", "Barış", "Erdem", "Koray", "Tuncay", "Selim", "Volkan",
        "Caner", "Uğur", "Gökhan", "Serhat", "Kerem", "Batuhan", "Alp", "Eren",
        "Taylan", "Cenk", "Umut", "Doruk", "Kuzey", "Emir", "Yiğit", "Çağrı",
        "Berk", "Mert", "Can", "Taha", "Alperen", "Görkem", "Utku", "Aras",
        "Tunahan", "Yağız", "Berke", "Polat", "Ozan", "Cemal", "Tarık", "Bilal",
        "Hamza", "Sami", "Orhan", "Nihat", "Cengiz", "Adem", "Yasin", "Sinan",
        "Samet", "Yunus", "Eray", "Atakan", "Doğukan", "Emirhan", "Efekan", "Çağatay",
        "Ferit", "Levent", "Harun", "Necati", "Oktay", "Rıza", "Şahin", "Tayfun",
        "Vedat", "Zafer", "Alihan", "Emircan"
    ];

    private static readonly string[] FemaleFirstNames =
    [
        "Fatma", "Ayşe", "Emine", "Hatice", "Zeynep", "Elif", "Merve", "Büşra",
        "Zehra", "Esra", "Havva", "Meryem", "Şerife", "Rabia", "Hanife", "Melek",
        "Hülya", "Derya", "Sibel", "Gül", "Seda", "Özlem", "Pınar", "Gözde",
        "Ceren", "İrem", "Selin", "Damla", "Aslı", "Ebru", "Cansu", "Burcu",
        "Tuğba", "Gamze", "Simge", "Melisa", "Başak", "Defne", "Dilan", "Ece",
        "Ezgi", "Buse", "Dilara", "Beyza", "Nazlı", "Sude", "Nehir", "Azra",
        "Ela", "Yaren", "İpek", "Beril", "Almina", "Asya", "Nisa", "Rüya",
        "Serra", "Meltem", "Sevgi", "Nalan", "Filiz", "Bahar", "Deniz", "Nil",
        "Cemre", "Duru", "Ilgın", "Kumru", "Mine", "Naz", "Oya", "Pelin",
        "Rana", "Sıla", "Tuğçe", "Ülkü", "Vildan", "Yeliz", "Zülal", "Ayla",
        "Berna", "Ceyda", "Didem", "Elçin", "Funda", "Gonca", "Hande", "Irmak",
        "Jale", "Kardelen", "Lale", "Miray", "Nur", "Özge", "Perihan", "Reyhan",
        "Songül", "Tülay", "Umay", "Yasemin"
    ];

    private static readonly string[] Surnames =
    [
        "Yılmaz", "Kaya", "Demir", "Çelik", "Şahin", "Yıldız", "Yıldırım", "Öztürk",
        "Aydın", "Özdemir", "Arslan", "Doğan", "Kılıç", "Aslan", "Çetin", "Kara",
        "Koç", "Kurt", "Özkan", "Şimşek", "Polat", "Korkmaz", "Yalçın", "Aktaş",
        "Erdoğan", "Ateş", "Güneş", "Çakmak", "Aksoy", "Kaplan", "Akın", "Taş",
        "Güler", "Bayrak", "Bulut", "Güngör", "Karataş", "Acar", "Sağlam", "Tuncer",
        "Avcı", "Serdar", "Bozkurt", "Başaran", "Ceylan", "Duman", "Erdem", "Gündüz",
        "İnan", "Keskin", "Oral", "Tekin", "Ünal", "Yavuz", "Alkan", "Balcı",
        "Coşkun", "Dinç", "Ekinci", "Fırat", "Gökçe", "Işık", "Kahraman", "Mutlu",
        "Özen", "Sönmez", "Tan", "Uçar", "Vural", "Zengin", "Akdeniz", "Bayram",
        "Çakır", "Dağ", "Eker", "Gedik", "Han", "Keleş", "Narin", "Parlak",
        "Savaş", "Toprak", "Ulusoy", "Vardar", "Yaman", "Altın", "Bakır", "Candan",
        "Duran", "Erol", "Güven", "Kalkan", "Mert", "Okumuş", "Pala", "Sezer",
        "Tekbaş", "Uysal", "Yazıcı", "Zenginler", "Akyüz", "Bilgin", "Çoban",
        "Demirci", "Elmas", "Genç", "Karakaş", "Karaman", "Koçak", "Kuzu",
        "Mercan", "Oğuz", "Peker", "Sarı", "Topal", "Uzun", "Yüksel", "Akgün",
        "Boz", "Çınar", "Diker", "Ercan", "Güneyli", "Karagöz", "Kızıl", "Okur",
        "Öğüt", "Önal", "Sevim", "Taner", "Yılmazer", "Arıkan", "Bayar", "Çetinkaya",
        "Dönmez", "Erdal", "Fidan", "Göker", "Ilıcalı", "Kalaycı", "Özbek",
        "Poyraz", "Sarıkaya", "Şen", "Tanrıverdi", "Üstün", "Varışlı", "Yörük",
        "Aksu", "Bircan", "Candemir", "Doğru", "Eser", "Gürbüz", "Kocaman",
        "Nalçacı", "Oğuzhan", "Solmaz", "Turhan", "Yalın", "Arslantürk", "Bakırcı",
        "Cesur", "Dursun", "Ergin", "Güçlü", "Kahveci", "Mollaer", "Öksüz",
        "Soylu", "Tosun", "Uyanık", "Veli", "Yurdakul"
    ];
}
