using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

/// <summary>
/// Auto-schedule algoritmasını gerçek üniversite verisiyle test etmek için kullanılır.
/// Mühendislik fakültesinin Güz 2024-25 + Bahar 2025-26 dönem verilerini içerir.
/// Section'lara hoca atanmaz (hoca kısıtları test sırasında pasifte).
///
/// Idempotent: tekrar çağrılırsa eksikleri tamamlar, var olanlara dokunmaz.
/// Tüm Classroom/Course kayıtları admin UI'dan yönetilebilir (hardcoded değil).
/// </summary>
public static partial class SeedData
{
    // ── Eklenmiş ekstra derslikler (artık silinecek) ─────────────────
    // İlk plan kullanıcının verdiği bahar verisindeki "C 053, C 056, C 074, C 075, L142, LH5"
    // odalarını eklemekti. Sonradan kullanıcı net etti: mevcut 14 derslik yeter, ekleme yok.
    // Bu liste artık SİLME hedefi olarak kullanılıyor (idempotent removal).
    private static readonly string[] _testExtraClassroomsToRemove = new[]
    {
        "C 053", "C 056", "C 074", "C 075", "L142 EEE LAB-2", "LH5 Lab",
    };

    // ── Course tanımları (110 unique) ────────────────────────────────
    // Bölüm eşleştirmesi:
    //   BIO/BIY/FIZ/MAT/MATH/PHY → TBK (Temel Bilimler Koordinatörlüğü)
    //   BM → BMTR (Bilgisayar Müh. Türkçe)
    //   CENG → BM (Bilgisayar Müh. İngilizce)
    //   EEE → EEM (Elektrik Elektronik Müh.)
    //   IE → EM (Endüstri Müh.)
    //   ISE → BSM (Bilişim Sistemleri Müh.)
    //   SENG → YM (Yazılım Müh.)
    private record TestCourseDef(
        string Code, string Name, string DeptCode,
        int TheoryHours, int LabHours,
        ClassroomType TheoryType, ClassroomType? LabType);

    private static readonly TestCourseDef[] _testCourses = new[]
    {
        // ── BIO/BIY (TBK) ──
        new TestCourseDef("BIO 101", "Biology I", "TBK", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("BIY 101", "Biyoloji I", "TBK", 3, 0, ClassroomType.Lecture, null),

        // ── FIZ/PHY (TBK) ──
        new TestCourseDef("FIZ 101", "Fizik I", "TBK", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("FIZ 102", "Fizik II", "TBK", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("FIZ 103", "Fizik Lab I", "TBK", 0, 2, ClassroomType.Lab, ClassroomType.Lab),
        new TestCourseDef("FIZ 104", "Fizik Lab II", "TBK", 0, 2, ClassroomType.Lab, ClassroomType.Lab),
        new TestCourseDef("PHY 101", "Physics I", "TBK", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("PHY 102", "Physics II", "TBK", 3, 0, ClassroomType.Amphitheater, null),
        new TestCourseDef("PHY 103", "Physics Lab I", "TBK", 0, 2, ClassroomType.Lab, ClassroomType.Lab),
        new TestCourseDef("PHY 104", "Physics Lab II", "TBK", 0, 2, ClassroomType.Lab, ClassroomType.Lab),

        // ── MAT/MATH (TBK) ──
        new TestCourseDef("MAT 101", "Matematik I", "TBK", 4, 0, ClassroomType.Lecture, null),
        new TestCourseDef("MAT 102", "Matematik II", "TBK", 4, 0, ClassroomType.Lecture, null),
        new TestCourseDef("MAT 121", "Lineer Cebir", "TBK", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("MAT 122", "Diferansiyel Denklemler", "TBK", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("MAT 206", "Olasilik", "TBK", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("MAT 224", "Sayisal Analiz", "TBK", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("MATH 101", "Calculus I", "TBK", 4, 0, ClassroomType.Lecture, null),
        new TestCourseDef("MATH 102", "Calculus II", "TBK", 4, 0, ClassroomType.Amphitheater, null),
        new TestCourseDef("MATH 201", "Linear Algebra", "TBK", 4, 0, ClassroomType.Amphitheater, null),
        new TestCourseDef("MATH 206", "Probability", "TBK", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("MATH 213", "Differential Equations", "TBK", 3, 0, ClassroomType.Amphitheater, null),
        new TestCourseDef("MATH 215", "Numerical Analysis", "TBK", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("MATH 224", "Discrete Math", "TBK", 3, 0, ClassroomType.Lecture, null),

        // ── BM (BMTR) ──
        new TestCourseDef("BM 101", "Bilgisayar Muhendisligine Giris", "BMTR", 3, 2, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("BM 102", "Programlama I", "BMTR", 3, 4, ClassroomType.Lecture, ClassroomType.Lab),  // Onaylı: 3T+4L
        new TestCourseDef("BM 203", "Programlama II", "BMTR", 3, 2, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("BM 214", "Algoritma", "BMTR", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("BM 223", "Veri Yapilari", "BMTR", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("BM 256", "Isletim Sistemleri", "BMTR", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("BM 264", "Yazilim Muhendisligi", "BMTR", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("BM 281", "Sayisal Tasarim", "BMTR", 3, 2, ClassroomType.Amphitheater, ClassroomType.Lab),
        new TestCourseDef("BM 324", "Veritabani", "BMTR", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("BM 332", "Bilgisayar Aglari", "BMTR", 3, 6, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("BM 336", "Yapay Zeka", "BMTR", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("BM 384", "Muhendislik Projesi", "BMTR", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("BM 424", "Bitirme Projesi", "BMTR", 6, 0, ClassroomType.Lecture, null),

        // ── CENG (BM) ──
        new TestCourseDef("CENG 101", "Intro to Computer Engineering", "BM", 3, 2, ClassroomType.Amphitheater, ClassroomType.Lab),
        new TestCourseDef("CENG 102", "Programming I", "BM", 3, 4, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("CENG 213", "Data Structures", "BM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("CENG 214", "Discrete Math (CENG)", "BM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("CENG 223", "Object Oriented Programming", "BM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("CENG 241", "Logic Design", "BM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("CENG 256", "Operating Systems", "BM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("CENG 332", "Computer Networks", "BM", 3, 2, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("CENG 336", "Artificial Intelligence", "BM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("CENG 384", "Senior Project I", "BM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("CENG 424", "Compilers", "BM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("CENG 446", "Cloud Computing", "BM", 3, 0, ClassroomType.Lecture, null),

        // ── EEE (EEM) ──
        new TestCourseDef("EEE 102", "Intro to EEE", "EEM", 2, 2, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("EEE 201", "Circuits I", "EEM", 4, 0, ClassroomType.Lecture, null),
        new TestCourseDef("EEE 202", "Circuits II", "EEM", 4, 0, ClassroomType.Lecture, null),
        new TestCourseDef("EEE 203", "Digital Logic", "EEM", 3, 2, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("EEE 204", "Electronics I", "EEM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("EEE 205", "Lab Course I", "EEM", 2, 1, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("EEE 208", "Signals and Systems", "EEM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("EEE 212", "Circuit Theory Lab", "EEM", 0, 2, ClassroomType.Lab, ClassroomType.Lab),
        new TestCourseDef("EEE 236", "Electromagnetics", "EEM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("EEE 281", "Microprocessors", "EEM", 3, 2, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("EEE 301", "Control Systems", "EEM", 3, 2, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("EEE 302", "Signals II", "EEM", 3, 2, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("EEE 303", "Power Systems", "EEM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("EEE 304", "Communications I", "EEM", 3, 2, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("EEE 305", "Microelectronics", "EEM", 3, 2, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("EEE 306", "Electronics Lab", "EEM", 3, 2, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("EEE 309", "Power Electronics", "EEM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("EEE 331", "Control Lab", "EEM", 3, 2, ClassroomType.Lecture, ClassroomType.Lab),
        new TestCourseDef("EEE 332", "Digital Signal Processing", "EEM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("EEE 421", "VLSI Design", "EEM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("EEE 455", "Wireless Communications", "EEM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("EEE 468", "Renewable Energy", "EEM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("EEE 472", "Digital Communications", "EEM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("EEE 482", "Senior Lab", "EEM", 0, 3, ClassroomType.Lab, ClassroomType.Lab),
        new TestCourseDef("EEE 484", "Capstone", "EEM", 3, 0, ClassroomType.Lecture, null),

        // ── IE (EM) ──
        new TestCourseDef("IE 101", "Intro to Industrial Eng", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 112", "Lab Course IE", "EM", 0, 3, ClassroomType.Lab, ClassroomType.Lab),
        new TestCourseDef("IE 202", "Statistics I", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 223", "Engineering Economy", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 224", "Optimization", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 227", "Production Systems", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 228", "Quality Management", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 232", "Inventory Management", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 241", "Operations Research I", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 242", "Operations Research II", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 301", "Production Planning", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 324", "Simulation", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 325", "Quality Control", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 327", "Logistics", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 333", "Supply Chain", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 346", "Decision Analysis", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 362", "Engineering Management", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 365", "Lean Manufacturing", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 366", "Stochastic Models", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 370", "Decision Support Systems", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 376", "Project Management", "EM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("IE 463", "Simulation Lab", "EM", 0, 3, ClassroomType.Lab, ClassroomType.Lab),
        new TestCourseDef("IE 467", "IE Lab", "EM", 0, 3, ClassroomType.Lab, ClassroomType.Lab),
        new TestCourseDef("IE 469", "IE Lab II", "EM", 0, 3, ClassroomType.Lab, ClassroomType.Lab),
        new TestCourseDef("IE 499", "Senior Design", "EM", 2, 0, ClassroomType.Lecture, null),

        // ── ISE (BSM) ──
        new TestCourseDef("ISE 211", "Database Design", "BSM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("ISE 222", "Web Technologies", "BSM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("ISE 322", "Business Intelligence", "BSM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("ISE 332", "Software Systems", "BSM", 6, 0, ClassroomType.Lecture, null),
        new TestCourseDef("ISE 350", "Enterprise Resource Planning", "BSM", 3, 0, ClassroomType.Lecture, null),

        // ── SENG (YM) ──
        new TestCourseDef("SENG 264", "Software Engineering", "YM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("SENG 322", "Software Quality", "YM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("SENG 324", "Web Applications", "YM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("SENG 413", "Distributed Systems", "YM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("SENG 420", "Mobile Development", "YM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("SENG 442", "Software Testing", "YM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("SENG 448", "Cloud Native", "YM", 3, 0, ClassroomType.Lecture, null),
        new TestCourseDef("SENG 501", "Advanced Software Engineering", "YM", 3, 0, ClassroomType.Lecture, null),
    };

    // ── Section atamaları (162 toplam) ───────────────────────────────
    // (CourseCode, Year, TermType, Sections, Capacity)
    // Capacity = teorik odanın kapasitesi (kullanıcının verdiği veriden)
    private record TestSectionDef(string CourseCode, int Year, TermType Type, int Count, int Capacity);

    private static readonly TestSectionDef[] _testSections = new[]
    {
        // ── GÜZ 2024-25 (45 ders / 72 section) ──
        new TestSectionDef("BIO 101", 2024, TermType.Fall, 3, 70),
        new TestSectionDef("BM 101", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("BM 203", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("BM 223", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("BM 281", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("CENG 101", 2024, TermType.Fall, 5, 70),
        new TestSectionDef("CENG 223", 2024, TermType.Fall, 4, 70),
        new TestSectionDef("CENG 241", 2024, TermType.Fall, 3, 70),
        new TestSectionDef("EEE 201", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("EEE 203", 2024, TermType.Fall, 4, 70),
        new TestSectionDef("EEE 205", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("EEE 281", 2024, TermType.Fall, 2, 70),
        new TestSectionDef("EEE 301", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("EEE 303", 2024, TermType.Fall, 1, 50),
        new TestSectionDef("EEE 305", 2024, TermType.Fall, 1, 50),
        new TestSectionDef("EEE 309", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("EEE 331", 2024, TermType.Fall, 1, 50),
        new TestSectionDef("EEE 421", 2024, TermType.Fall, 1, 50),
        new TestSectionDef("EEE 468", 2024, TermType.Fall, 1, 50),
        new TestSectionDef("EEE 472", 2024, TermType.Fall, 1, 50),
        new TestSectionDef("FIZ 101", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("FIZ 103", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("IE 101", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("IE 223", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("IE 227", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("IE 241", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("IE 301", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("IE 325", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("IE 327", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("IE 333", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("IE 365", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("IE 463", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("IE 467", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("IE 469", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("IE 499", 2024, TermType.Fall, 1, 30),
        new TestSectionDef("ISE 211", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("MAT 101", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("MATH 101", 2024, TermType.Fall, 5, 70),
        new TestSectionDef("MATH 201", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("MATH 213", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("MATH 215", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("MATH 224", 2024, TermType.Fall, 1, 70),
        new TestSectionDef("PHY 101", 2024, TermType.Fall, 5, 70),
        new TestSectionDef("PHY 103", 2024, TermType.Fall, 5, 30),
        new TestSectionDef("SENG 442", 2024, TermType.Fall, 1, 70),

        // ── BAHAR 2025-26 (70 ders / 90 section) ──
        new TestSectionDef("BIO 101", 2025, TermType.Spring, 1, 48),
        new TestSectionDef("BIY 101", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("BM 102", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("BM 214", 2025, TermType.Spring, 1, 48),
        new TestSectionDef("BM 256", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("BM 264", 2025, TermType.Spring, 1, 31),
        new TestSectionDef("BM 324", 2025, TermType.Spring, 1, 63),
        new TestSectionDef("BM 332", 2025, TermType.Spring, 1, 48),
        new TestSectionDef("BM 336", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("BM 384", 2025, TermType.Spring, 1, 31),
        new TestSectionDef("BM 424", 2025, TermType.Spring, 1, 31),
        new TestSectionDef("CENG 101", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("CENG 102", 2025, TermType.Spring, 3, 70),
        new TestSectionDef("CENG 213", 2025, TermType.Spring, 1, 30),
        new TestSectionDef("CENG 214", 2025, TermType.Spring, 2, 70),
        new TestSectionDef("CENG 256", 2025, TermType.Spring, 2, 70),
        new TestSectionDef("CENG 332", 2025, TermType.Spring, 1, 48),
        new TestSectionDef("CENG 336", 2025, TermType.Spring, 3, 70),
        new TestSectionDef("CENG 384", 2025, TermType.Spring, 2, 70),
        new TestSectionDef("CENG 424", 2025, TermType.Spring, 1, 48),
        new TestSectionDef("CENG 446", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("EEE 102", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("EEE 202", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("EEE 203", 2025, TermType.Spring, 1, 48),
        new TestSectionDef("EEE 204", 2025, TermType.Spring, 1, 65),
        new TestSectionDef("EEE 208", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("EEE 212", 2025, TermType.Spring, 1, 30),
        new TestSectionDef("EEE 236", 2025, TermType.Spring, 1, 63),
        new TestSectionDef("EEE 302", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("EEE 304", 2025, TermType.Spring, 1, 48),
        new TestSectionDef("EEE 306", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("EEE 332", 2025, TermType.Spring, 1, 31),
        new TestSectionDef("EEE 455", 2025, TermType.Spring, 1, 31),
        new TestSectionDef("EEE 482", 2025, TermType.Spring, 1, 30),
        new TestSectionDef("EEE 484", 2025, TermType.Spring, 1, 31),
        new TestSectionDef("FIZ 102", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("FIZ 104", 2025, TermType.Spring, 1, 30),
        new TestSectionDef("IE 112", 2025, TermType.Spring, 1, 40),
        new TestSectionDef("IE 202", 2025, TermType.Spring, 1, 65),
        new TestSectionDef("IE 224", 2025, TermType.Spring, 1, 65),
        new TestSectionDef("IE 228", 2025, TermType.Spring, 1, 65),
        new TestSectionDef("IE 232", 2025, TermType.Spring, 1, 65),
        new TestSectionDef("IE 242", 2025, TermType.Spring, 1, 65),
        new TestSectionDef("IE 324", 2025, TermType.Spring, 1, 65),
        new TestSectionDef("IE 346", 2025, TermType.Spring, 1, 65),
        new TestSectionDef("IE 362", 2025, TermType.Spring, 1, 65),
        new TestSectionDef("IE 366", 2025, TermType.Spring, 1, 65),
        new TestSectionDef("IE 370", 2025, TermType.Spring, 1, 65),
        new TestSectionDef("IE 376", 2025, TermType.Spring, 1, 65),
        new TestSectionDef("ISE 222", 2025, TermType.Spring, 1, 31),
        new TestSectionDef("ISE 322", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("ISE 332", 2025, TermType.Spring, 1, 30),
        new TestSectionDef("MAT 102", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("MAT 121", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("MAT 122", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("MAT 206", 2025, TermType.Spring, 1, 39),
        new TestSectionDef("MAT 224", 2025, TermType.Spring, 1, 39),
        new TestSectionDef("MATH 101", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("MATH 102", 2025, TermType.Spring, 3, 70),
        new TestSectionDef("MATH 206", 2025, TermType.Spring, 3, 63),
        new TestSectionDef("MATH 224", 2025, TermType.Spring, 2, 70),
        new TestSectionDef("PHY 102", 2025, TermType.Spring, 3, 70),
        new TestSectionDef("PHY 104", 2025, TermType.Spring, 3, 30),
        new TestSectionDef("SENG 264", 2025, TermType.Spring, 3, 70),
        new TestSectionDef("SENG 322", 2025, TermType.Spring, 1, 48),
        new TestSectionDef("SENG 324", 2025, TermType.Spring, 2, 48),
        new TestSectionDef("SENG 413", 2025, TermType.Spring, 1, 31),
        new TestSectionDef("SENG 420", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("SENG 448", 2025, TermType.Spring, 1, 70),
        new TestSectionDef("SENG 501", 2025, TermType.Spring, 1, 65),
    };

    // 7 hoca kısıt kodu — test sürecinde pasifte
    // (Memory: project_autoschedule_test_disabled_constraints.md — test bittiğinde geri açılacak)
    private static readonly string[] _testInstructorConstraintCodes =
        new[] { "H2", "H-WL", "H-DL", "H-DO", "H15", "S2", "S8" };

    // ── Public Orchestration Methods ─────────────────────────────────

    public static async Task<AutoScheduleTestDataStatus> SeedAutoScheduleTestDataAsync(AppDbContext context)
    {
        var status = new AutoScheduleTestDataStatus();
        await EnsureTermsAsync(context);
        status.ClassroomsAdded = await RemoveExtraClassroomsAsync(context); // negatif sayı = silinen oda sayısı
        await UpdateAmphitheaterTypesAsync(context);
        await EnsureClassroomTermAssignmentsAsync(context);
        await EnsureTimeSlotsForTestTermsAsync(context);
        status.CoursesAdded = await EnsureCoursesAsync(context);
        status.SectionsAdded = await EnsureSectionsAsync(context);
        status.ConstraintsDisabled = await DisableInstructorConstraintsAsync(context);
        var current = await GetAutoScheduleTestDataStatusAsync(context);
        status.FallSectionsCount = current.FallSectionsCount;
        status.SpringSectionsCount = current.SpringSectionsCount;
        status.CoursesCount = current.CoursesCount;
        status.ExtraClassroomsCount = current.ExtraClassroomsCount;
        status.InstructorConstraintsDisabled = current.InstructorConstraintsDisabled;
        return status;
    }

    public static async Task<int> RemoveAutoScheduleTestDataAsync(AppDbContext context)
    {
        var fallTermId = await context.AcademicTerms
            .Where(t => t.Year == 2024 && t.TermType == TermType.Fall)
            .Select(t => t.Id).FirstOrDefaultAsync();
        var springTermId = await context.AcademicTerms
            .Where(t => t.Year == 2025 && t.TermType == TermType.Spring)
            .Select(t => t.Id).FirstOrDefaultAsync();

        if (fallTermId == 0 && springTermId == 0) return 0;

        var testCourseCodes = _testCourses.Select(c => c.Code).ToHashSet();

        var sectionsToRemove = await context.CourseSections
            .Include(s => s.Course)
            .Where(s => (s.TermId == fallTermId || s.TermId == springTermId)
                        && testCourseCodes.Contains(s.Course.Code))
            .ToListAsync();

        if (sectionsToRemove.Count == 0)
        {
            // Hoca kısıtlarını yine de geri aktif et
            await ReenableInstructorConstraintsAsync(context);
            return 0;
        }

        var sectionIdsSet = sectionsToRemove.Select(s => s.Id).ToHashSet();
        var schedulesToRemove = await context.SectionSchedules
            .Where(ss => sectionIdsSet.Contains(ss.CourseSectionId))
            .ToListAsync();

        context.SectionSchedules.RemoveRange(schedulesToRemove);
        context.CourseSections.RemoveRange(sectionsToRemove);
        await ReenableInstructorConstraintsAsync(context);
        await context.SaveChangesAsync();
        return sectionsToRemove.Count;
    }

    public static async Task<AutoScheduleTestDataStatus> GetAutoScheduleTestDataStatusAsync(AppDbContext context)
    {
        var status = new AutoScheduleTestDataStatus();

        var fallTermId = await context.AcademicTerms
            .Where(t => t.Year == 2024 && t.TermType == TermType.Fall)
            .Select(t => t.Id).FirstOrDefaultAsync();
        var springTermId = await context.AcademicTerms
            .Where(t => t.Year == 2025 && t.TermType == TermType.Spring)
            .Select(t => t.Id).FirstOrDefaultAsync();

        var testCourseCodes = _testCourses.Select(c => c.Code).ToHashSet();

        if (fallTermId != 0)
        {
            status.FallSectionsCount = await context.CourseSections
                .Include(s => s.Course)
                .Where(s => s.TermId == fallTermId && testCourseCodes.Contains(s.Course.Code))
                .CountAsync();
        }

        if (springTermId != 0)
        {
            status.SpringSectionsCount = await context.CourseSections
                .Include(s => s.Course)
                .Where(s => s.TermId == springTermId && testCourseCodes.Contains(s.Course.Code))
                .CountAsync();
        }

        status.CoursesCount = await context.Courses
            .Where(c => testCourseCodes.Contains(c.Code))
            .CountAsync();

        // Test başında bu 6 dersliği silmiş olmalı (artık ekleme yok). 0 = silindi.
        var extraNames = _testExtraClassroomsToRemove.ToHashSet();
        status.ExtraClassroomsCount = await context.Classrooms
            .Where(c => extraNames.Contains(c.Name))
            .CountAsync();

        status.InstructorConstraintsDisabled = await context.ConstraintDefinitions
            .Where(c => _testInstructorConstraintCodes.Contains(c.Code) && !c.IsEnabled)
            .CountAsync();

        return status;
    }

    // ── Private helpers ──────────────────────────────────────────────

    /// <summary>
    /// Test verisi için gerekli iki term'i (Güz 2024-25 + Bahar 2025-26) garanti eder.
    /// DB'de yoksa minimal alanlarla oluşturur. Admin sonradan UI'dan düzenleyebilir.
    /// </summary>
    private static async Task EnsureTermsAsync(AppDbContext context)
    {
        var fallExists = await context.AcademicTerms
            .AsNoTracking()
            .AnyAsync(t => t.Year == 2024 && t.TermType == TermType.Fall);

        var springExists = await context.AcademicTerms
            .AsNoTracking()
            .AnyAsync(t => t.Year == 2025 && t.TermType == TermType.Spring);

        if (!fallExists)
        {
            try
            {
                context.AcademicTerms.Add(new AcademicTerm
                {
                    Name = "2024-2025 Güz",
                    ShortName = "24-25 Güz",
                    EnglishName = "Fall 2024-2025",
                    ShortEnglishName = "24-25 Fall",
                    Year = 2024,
                    TermType = TermType.Fall,
                    TermStart = new DateTime(2024, 9, 16, 0, 0, 0, DateTimeKind.Utc),
                    TermEnd = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                    IsCancelled = false,
                    YoksisStatus = YoksisStatus.Inactive,
                    CreatedAt = DateTime.UtcNow,
                });
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                context.ChangeTracker.Clear();
            }
        }

        if (!springExists)
        {
            try
            {
                context.AcademicTerms.Add(new AcademicTerm
                {
                    Name = "2025-2026 Bahar",
                    ShortName = "25-26 Bahar",
                    EnglishName = "Spring 2025-2026",
                    ShortEnglishName = "25-26 Spring",
                    Year = 2025,
                    TermType = TermType.Spring,
                    TermStart = new DateTime(2026, 2, 23, 0, 0, 0, DateTimeKind.Utc),
                    TermEnd = new DateTime(2026, 6, 5, 0, 0, 0, DateTimeKind.Utc),
                    IsCancelled = false,
                    YoksisStatus = YoksisStatus.Inactive,
                    CreatedAt = DateTime.UtcNow,
                });
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                context.ChangeTracker.Clear();
            }
        }
    }

    /// <summary>
    /// Test term'leri için TimeSlot grid'ini garanti eder. SystemConfig parametrelerinden
    /// üretir (Schedule.SlotDurationMinutes, Schedule.MorningStartTime, vb.). Idempotent.
    /// </summary>
    private static async Task EnsureTimeSlotsForTestTermsAsync(AppDbContext context)
    {
        var fallTerm = await context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Year == 2024 && t.TermType == TermType.Fall);
        var springTerm = await context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Year == 2025 && t.TermType == TermType.Spring);

        var termIds = new List<int>();
        if (fallTerm != null) termIds.Add(fallTerm.Id);
        if (springTerm != null) termIds.Add(springTerm.Id);

        foreach (var termId in termIds)
        {
            var hasSlots = await context.TimeSlots
                .AsNoTracking()
                .AnyAsync(ts => ts.TermId == termId);
            if (hasSlots) continue;

            var slots = await GenerateTimeSlotsFromConfigAsync(context, termId);
            if (slots.Count > 0)
            {
                context.TimeSlots.AddRange(slots);
                try
                {
                    await context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    context.ChangeTracker.Clear();
                }
            }
        }
    }

    /// <summary>
    /// Tüm derslikleri MUH fakültesine + iki test term'e atar.
    /// Algoritma fakülteye atanmış derslikleri kullanır; bu eksikse hiçbir section yerleşmez.
    /// Idempotent — var olan atamaları atlar. Admin UI'dan elle değiştirilebilir.
    /// </summary>
    private static async Task EnsureClassroomTermAssignmentsAsync(AppDbContext context)
    {
        var muhFaculty = await context.Faculties
            .AsNoTracking()
            .FirstOrDefaultAsync(f => f.Code == "MUH");
        if (muhFaculty == null) return;

        var fallTerm = await context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Year == 2024 && t.TermType == TermType.Fall);
        var springTerm = await context.AcademicTerms
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.Year == 2025 && t.TermType == TermType.Spring);

        var classroomIds = await context.Classrooms
            .AsNoTracking()
            .Select(c => c.Id)
            .ToListAsync();

        var existingAssignments = await context.ClassroomTermAssignments
            .AsNoTracking()
            .Select(cta => new { cta.ClassroomId, cta.TermId, cta.FacultyId })
            .ToListAsync();
        var existingSet = existingAssignments
            .Select(a => (a.ClassroomId, a.TermId, a.FacultyId))
            .ToHashSet();

        var termIdsToAssign = new List<int>();
        if (fallTerm != null) termIdsToAssign.Add(fallTerm.Id);
        if (springTerm != null) termIdsToAssign.Add(springTerm.Id);

        foreach (var classroomId in classroomIds)
        {
            foreach (var termId in termIdsToAssign)
            {
                if (existingSet.Contains((classroomId, termId, muhFaculty.Id))) continue;
                try
                {
                    context.ClassroomTermAssignments.Add(new ClassroomTermAssignment
                    {
                        ClassroomId = classroomId,
                        TermId = termId,
                        FacultyId = muhFaculty.Id,
                    });
                    await context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    context.ChangeTracker.Clear();
                }
            }
        }
    }

    /// <summary>
    /// AMFİ 1 ve "Amfi" tip düzeltmesinin geri çekilmesi.
    ///
    /// İlk plan AMFİ 1 + Amfi'yi (DB'de Lecture) → Amphitheater'a güncellemekti
    /// (veri tutarlılığı amaçlı). Ama bu **algoritmik regresyon** yarattı:
    /// CandidateGenerator GeneralTeachingTypes set'i Lecture↔Amphitheater eşit sayıyor
    /// (workaround zaten çalışıyordu). Tip değişince 26 büyük Lecture section AMFİ 1'i
    /// kullanamadı (artık sadece Amphitheater istemleri için), Spring başarı 55→52'ye düştü.
    ///
    /// Bu metod **revert** yapar: Amphitheater'a güncellenmiş AMFİ 1/Amfi'yi Lecture'a geri çeker.
    /// Idempotent — ikinci çalıştırmada zaten Lecture, no-op. Test sonrası silinebilir.
    /// </summary>
    private static async Task UpdateAmphitheaterTypesAsync(AppDbContext context)
    {
        var amphitheaterNames = new[] { "AMFİ 1", "Amfi - Sınıf-Yeni-Kat Sıfır" };

        var classrooms = await context.Classrooms
            .Where(c => amphitheaterNames.Contains(c.Name) && c.Type == ClassroomType.Amphitheater)
            .ToListAsync();

        if (classrooms.Count == 0) return; // zaten Lecture (revert tamamlanmış veya hiç değişmemiş)

        foreach (var classroom in classrooms)
        {
            classroom.Type = ClassroomType.Lecture;
            classroom.UpdatedAt = DateTime.UtcNow;
        }
        await context.SaveChangesAsync();
    }

    /// <summary>
    /// Önceden test data seed'iyle eklenen 6 ekstra dersliği DB'den siler.
    /// Kullanıcı net etti: mevcut 14 derslik yeter, ekstra ekleme yok.
    /// İlişkili kayıtları (ClassroomTermAssignment, SectionSchedule.ClassroomId,
    /// CourseSection.ClassroomId) önce temizler. Idempotent.
    /// </summary>
    private static async Task<int> RemoveExtraClassroomsAsync(AppDbContext context)
    {
        var classroomsToRemove = await context.Classrooms
            .Where(c => _testExtraClassroomsToRemove.Contains(c.Name))
            .ToListAsync();

        if (classroomsToRemove.Count == 0) return 0;

        var classroomIds = classroomsToRemove.Select(c => c.Id).ToHashSet();

        // 1) ClassroomTermAssignment kayıtlarını sil
        var assignments = await context.ClassroomTermAssignments
            .Where(cta => classroomIds.Contains(cta.ClassroomId))
            .ToListAsync();
        if (assignments.Count > 0)
            context.ClassroomTermAssignments.RemoveRange(assignments);

        // 2) SectionSchedule.ClassroomId nullable, bu odalara işaret edenleri null'a çek
        var schedulesPointingHere = await context.SectionSchedules
            .Where(ss => ss.ClassroomId.HasValue && classroomIds.Contains(ss.ClassroomId.Value))
            .ToListAsync();
        foreach (var ss in schedulesPointingHere)
            ss.ClassroomId = null;

        // 3) CourseSection.ClassroomId nullable
        var sectionsPointingHere = await context.CourseSections
            .Where(cs => cs.ClassroomId.HasValue && classroomIds.Contains(cs.ClassroomId.Value))
            .ToListAsync();
        foreach (var cs in sectionsPointingHere)
            cs.ClassroomId = null;

        // 4) Şimdi classroom'ları sil
        context.Classrooms.RemoveRange(classroomsToRemove);

        try
        {
            await context.SaveChangesAsync();
            return classroomsToRemove.Count;
        }
        catch (DbUpdateException)
        {
            context.ChangeTracker.Clear();
            return 0;
        }
    }

    private static async Task<int> EnsureCoursesAsync(AppDbContext context)
    {
        // Mevcut kursları code → entity map'ine al (upsert için tracked)
        var existingByCode = await context.Courses
            .ToDictionaryAsync(c => c.Code, c => c, StringComparer.OrdinalIgnoreCase);

        var deptByCode = await context.Departments
            .AsNoTracking()
            .ToDictionaryAsync(d => d.Code, d => d.Id);

        var changed = 0;
        foreach (var def in _testCourses)
        {
            if (!deptByCode.TryGetValue(def.DeptCode, out var deptId))
            {
                throw new InvalidOperationException(
                    $"Department '{def.DeptCode}' not found for course '{def.Code}'. " +
                    "Run academic structure seed first (SeedAcademicStructureAsync).");
            }

            if (existingByCode.TryGetValue(def.Code, out var existing))
            {
                // Saat veya tür değişti mi? Test verisinin authoritative kaynağı _testCourses array'i;
                // burada güncelle ki yeniden seed çağrısı saat düzeltmesini DB'ye yansıtsın.
                var dirty = existing.Name != def.Name
                    || existing.TheoryHours != def.TheoryHours
                    || existing.LabHours != def.LabHours
                    || existing.TheoryClassroomType != def.TheoryType
                    || existing.LabClassroomType != def.LabType
                    || existing.DepartmentId != deptId;

                if (dirty)
                {
                    existing.Name = def.Name;
                    existing.TheoryHours = def.TheoryHours;
                    existing.LabHours = def.LabHours;
                    existing.Ects = (def.TheoryHours + def.LabHours) * 2;
                    existing.TheoryClassroomType = def.TheoryType;
                    existing.LabClassroomType = def.LabType;
                    existing.DepartmentId = deptId;
                    changed++;
                }
                continue;
            }

            try
            {
                var course = new Course
                {
                    Code = def.Code,
                    Name = def.Name,
                    DepartmentId = deptId,
                    TheoryHours = def.TheoryHours,
                    LabHours = def.LabHours,
                    Ects = (def.TheoryHours + def.LabHours) * 2,
                    TheoryClassroomType = def.TheoryType,
                    LabClassroomType = def.LabType,
                    CreatedAt = DateTime.UtcNow,
                    Status = CourseStatus.Active,
                    Language = CourseLanguage.Turkish,
                    TeachingMethod = TeachingMethod.FaceToFace,
                    Level = CourseLevel.Undergraduate,
                };
                context.Courses.Add(course);
                existingByCode[def.Code] = course;
                changed++;
            }
            catch (DbUpdateException)
            {
                context.ChangeTracker.Clear();
            }
        }

        if (changed > 0)
            await context.SaveChangesAsync();
        return changed;
    }

    private static async Task<int> EnsureSectionsAsync(AppDbContext context)
    {
        var courseByCode = await context.Courses.ToDictionaryAsync(c => c.Code, c => c.Id);

        var fallTermId = await context.AcademicTerms
            .Where(t => t.Year == 2024 && t.TermType == TermType.Fall)
            .Select(t => t.Id).FirstOrDefaultAsync();
        var springTermId = await context.AcademicTerms
            .Where(t => t.Year == 2025 && t.TermType == TermType.Spring)
            .Select(t => t.Id).FirstOrDefaultAsync();

        if (fallTermId == 0 || springTermId == 0)
        {
            throw new InvalidOperationException(
                "Fall 2024-25 or Spring 2025-26 term not found. Run terms seed first.");
        }

        var testCourseCodes = _testCourses.Select(c => c.Code).ToHashSet();

        // Mevcut tüm test section'larını çek — capacity update için tam liste lazım
        var existingTestSections = await context.CourseSections
            .Include(s => s.Course)
            .Where(s => (s.TermId == fallTermId || s.TermId == springTermId)
                        && testCourseCodes.Contains(s.Course.Code))
            .ToListAsync();

        // (TermId, CourseId) → mevcut section'lar listesi
        var existingByKey = existingTestSections
            .GroupBy(s => (s.TermId, s.CourseId))
            .ToDictionary(g => g.Key, g => g.OrderBy(s => s.SectionNumber).ToList());

        // (TermId, CourseId) → en yüksek SectionNumber
        var maxSectionByKey = existingByKey
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Max(s => s.SectionNumber));

        var added = 0;
        var capacityUpdated = 0;

        foreach (var def in _testSections)
        {
            if (!courseByCode.TryGetValue(def.CourseCode, out var courseId)) continue;
            var termId = def.Type == TermType.Fall ? fallTermId : springTermId;
            var key = (termId, courseId);

            // 1) Mevcut section'ların Capacity'sini güncelle (idempotent)
            if (existingByKey.TryGetValue(key, out var existingList))
            {
                foreach (var existing in existingList)
                {
                    if (existing.Capacity != def.Capacity && !existing.IsFinalized)
                    {
                        existing.Capacity = def.Capacity;
                        existing.UpdatedAt = DateTime.UtcNow;
                        capacityUpdated++;
                    }
                }
            }

            // 2) Yeni section ekle (eksik olanlar)
            var existingMax = maxSectionByKey.GetValueOrDefault(key, 0);
            if (existingMax >= def.Count) continue;

            for (var sn = existingMax + 1; sn <= def.Count; sn++)
            {
                try
                {
                    context.CourseSections.Add(new CourseSection
                    {
                        CourseId = courseId,
                        TermId = termId,
                        SectionNumber = sn,
                        Capacity = def.Capacity,
                        EnrolledCount = 0,
                        InstructorId = null,
                        ClassroomId = null,
                        Status = CourseSectionStatus.Active,
                        IsFinalized = false,
                        IsScheduleLocked = false,
                        IsPinned = false,
                        CreatedAt = DateTime.UtcNow,
                    });
                    await context.SaveChangesAsync();
                    added++;
                }
                catch (DbUpdateException)
                {
                    context.ChangeTracker.Clear();
                }
            }
        }

        // Capacity update'leri (eğer add yapılmadıysa) flush et
        if (capacityUpdated > 0)
        {
            try
            {
                await context.SaveChangesAsync();
            }
            catch (DbUpdateException)
            {
                context.ChangeTracker.Clear();
            }
        }
        return added;
    }

    private static async Task<int> DisableInstructorConstraintsAsync(AppDbContext context)
    {
        var constraints = await context.ConstraintDefinitions
            .Where(c => _testInstructorConstraintCodes.Contains(c.Code) && c.IsEnabled)
            .ToListAsync();

        if (constraints.Count == 0) return 0;

        foreach (var c in constraints) c.IsEnabled = false;
        await context.SaveChangesAsync();
        return constraints.Count;
    }

    private static async Task ReenableInstructorConstraintsAsync(AppDbContext context)
    {
        var constraints = await context.ConstraintDefinitions
            .Where(c => _testInstructorConstraintCodes.Contains(c.Code) && !c.IsEnabled)
            .ToListAsync();

        foreach (var c in constraints) c.IsEnabled = true;
        if (constraints.Count > 0)
            await context.SaveChangesAsync();
    }
}

public sealed class AutoScheduleTestDataStatus
{
    public int ClassroomsAdded { get; set; }
    public int CoursesAdded { get; set; }
    public int SectionsAdded { get; set; }
    public int ConstraintsDisabled { get; set; }
    public int FallSectionsCount { get; set; }
    public int SpringSectionsCount { get; set; }
    public int CoursesCount { get; set; }
    public int ExtraClassroomsCount { get; set; }
    public int InstructorConstraintsDisabled { get; set; }
}
