using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static partial class SeedData
{
    private static async Task SeedUsersAsync(AppDbContext context, UserManager<ApplicationUser> userManager)
    {
        // ============================================================
        // 1. ADMIN
        // ============================================================
        await EnsureUserAsync(userManager, "admin@ankarabilim.edu.tr", "System", "Admin", "Admin123!", "SystemAdministrator");

        // ============================================================
        // 2. INSTRUCTORS (36 total)
        // ============================================================
        var instructorData = new (string First, string Last, string Email, string DeptCode, string Title)[]
        {
            // BM (16 instructors)
            ("Ender", "Sevinç", "ender.sevinc@ankarabilim.edu.tr", "BM", "Prof. Dr."),
            ("Hakan", "Çağlar", "hakan.caglar@ankarabilim.edu.tr", "BM", "Prof. Dr."),
            ("M. Nedim", "Alpdemir", "mnedim.alpdemir@ankarabilim.edu.tr", "BM", "Doç. Dr."),
            ("Ayhan", "Akbaş", "ayhan.akbas@ankarabilim.edu.tr", "BM", "Doç. Dr."),
            ("Mesut", "Ünlü", "mesut.unlu@ankarabilim.edu.tr", "BM", "Dr."),
            ("Ekrem Çağlar", "Yılmaz", "ekremcaglar.yilmaz@ankarabilim.edu.tr", "BM", "Dr."),
            ("Yüksel", "Arslan", "yuksel.arslan@ankarabilim.edu.tr", "BM", "Dr."),
            ("Sarper", "Alkan", "sarper.alkan@ankarabilim.edu.tr", "BM", "Dr."),
            ("Hasan", "Karaaslan", "hasan.karaaslan@ankarabilim.edu.tr", "BM", "Dr."),
            ("Seher", "Fişekçi Par", "seher.fisekcipar@ankarabilim.edu.tr", "BM", "Dr."),
            ("Emre", "Alıç", "emre.alic@ankarabilim.edu.tr", "BM", "Dr."),
            ("Erkan", "Dilaveroğlu", "erkan.dilaveroglu@ankarabilim.edu.tr", "BM", "Dr."),
            ("Sali", "Yıldırım", "sali.yildirim@ankarabilim.edu.tr", "BM", "Dr."),
            ("Ömer Çağrı", "Dala", "omercagri.dala@ankarabilim.edu.tr", "BM", "Arş. Gör."),
            ("Merve", "Onaran", "merve.onaran@ankarabilim.edu.tr", "BM", "Arş. Gör."),
            ("Cansu", "Yörük", "cansu.yoruk@ankarabilim.edu.tr", "BM", "Arş. Gör."),
            ("Mehmet", "Öztürk", "ogretmen@ankarabilim.edu.tr", "BM", "Dr."),
            // EM (10 instructors)
            ("Yavuz Selim", "Özdemir", "yavuzselim.ozdemir@ankarabilim.edu.tr", "EM", "Doç. Dr."),
            ("Benhür", "Satır", "benhur.satir@ankarabilim.edu.tr", "EM", "Doç. Dr."),
            ("Orhan", "Gerdan", "orhan.gerdan@ankarabilim.edu.tr", "EM", "Dr."),
            ("Arda", "Alp", "arda.alp@ankarabilim.edu.tr", "EM", "Dr."),
            ("Burcu Devrim", "İçtenbaş", "burcudevrim.ictenbas@ankarabilim.edu.tr", "EM", "Dr."),
            ("Mustafa", "Akkaya", "mustafa.akkaya@ankarabilim.edu.tr", "EM", "Dr."),
            ("Baycan", "İbrahimoğlu", "baycan.ibrahimoglu@ankarabilim.edu.tr", "EM", "Emeritus Prof. Dr."),
            ("Özgür", "Erol", "ozgur.erol@ankarabilim.edu.tr", "EM", "Doç. Dr."),
            ("Deniz", "Okul", "deniz.okul@ankarabilim.edu.tr", "EM", "Dr."),
            ("Güneşcan", "Çetik", "gunescan.cetik@ankarabilim.edu.tr", "EM", "Arş. Gör."),
            // EEM (9 instructors)
            ("Halim Haldun", "Göktaş", "halimhaldun.goktas@ankarabilim.edu.tr", "EEM", "Prof. Dr."),
            ("Alas", "Eleyan", "alas.eleyan@ankarabilim.edu.tr", "EEM", "Doç. Dr."),
            ("Hamed", "Kaghazchi", "hamed.kaghazchi@ankarabilim.edu.tr", "EEM", "Dr."),
            ("Ercüment", "Karapınar", "ercument.karapinar@ankarabilim.edu.tr", "EEM", "Dr."),
            ("Ayşe", "Özdemir", "ayse.ozdemir@ankarabilim.edu.tr", "EEM", "Dr."),
            ("Vedat", "Tanrıverdi", "vedat.tanriverdi@ankarabilim.edu.tr", "EEM", "Dr."),
            ("Yusuf", "Uzun", "yusuf.uzun@ankarabilim.edu.tr", "EEM", "Dr."),
            ("Parisa", "Akar", "parisa.akar@ankarabilim.edu.tr", "EEM", "Dr."),
            ("Taha", "Selik", "taha.selik@ankarabilim.edu.tr", "EEM", "Arş. Gör."),
        };

        var depts = context.Departments.ToDictionary(d => d.Code, d => d.Id);
        foreach (var (first, last, email, deptCode, title) in instructorData)
        {
            var user = await EnsureUserAsync(userManager, email, first, last, "Ogretmen123!", "Instructor");
            if (!context.Instructors.Any(i => i.UserId == user.Id))
            {
                context.Instructors.Add(new Instructor
                {
                    UserId = user.Id,
                    DepartmentId = depts[deptCode],
                    Title = title,
                    MaxWeeklyHours = 20
                });
            }
        }
        await context.SaveChangesAsync();

        // ============================================================
        // 3. STUDENTS (18 total)
        // ============================================================
        // (first, last, email, studentNum, nationalId, progCode, cgpa, sgpa, completedSem, isScholarship, entryYear, isRetained)
        var studentData = new (string First, string Last, string Email, string Num, string NatId,
            string ProgCode, decimal Cgpa, decimal Sgpa, int Semesters, bool Scholar, int Year)[]
        {
            // BM-L: başarılı / sınırda / başarısız
            ("Ayşe", "Yılmaz", "ogrenci@ankarabilim.edu.tr", "2201001", "1220100100000", "BM-L", 3.20m, 3.20m, 7, true, 2022),
            ("Emre", "Kaya", "ogrenci2@ankarabilim.edu.tr", "2401002", "1240100200000", "BM-L", 1.85m, 1.85m, 3, false, 2024),
            ("Burak", "Demir", "ogrenci3@ankarabilim.edu.tr", "2501003", "1250100300000", "BM-L", 1.40m, 1.40m, 1, false, 2025),
            // BMTR-L: başarılı / sınırda / başarısız
            ("Zeynep", "Arslan", "ogrenci4@ankarabilim.edu.tr", "2302001", "1230200100000", "BMTR-L", 3.10m, 3.10m, 5, false, 2023),
            ("Kenan", "Yıldız", "ogrenci5@ankarabilim.edu.tr", "2402002", "1240200200000", "BMTR-L", 1.90m, 1.90m, 3, false, 2024),
            ("Elif", "Ozdemir", "ogrenci6@ankarabilim.edu.tr", "2202003", "1220200300000", "BMTR-L", 1.50m, 1.50m, 7, false, 2022),
            // BSM-L: başarılı / sınırda / başarısız
            ("Sergen", "Yalcin", "ogrenci7@ankarabilim.edu.tr", "2203001", "1220300100000", "BSM-L", 3.50m, 3.50m, 7, false, 2022),
            ("Arthur", "Morgan", "ogrenci8@ankarabilim.edu.tr", "2303002", "1230300200000", "BSM-L", 1.80m, 1.80m, 5, false, 2023),
            ("Orkun", "Kokcu", "ogrenci9@ankarabilim.edu.tr", "2403003", "1240300300000", "BSM-L", 1.45m, 1.45m, 3, false, 2024),
            // EM-L: başarılı / sınırda / başarısız
            ("Ceren", "Yildiz", "ogrenci10@ankarabilim.edu.tr", "2204001", "1220400100000", "EM-L", 2.80m, 2.80m, 7, false, 2022),
            ("Kaan", "Celik", "ogrenci11@ankarabilim.edu.tr", "2504002", "1250400200000", "EM-L", 1.75m, 1.75m, 1, false, 2025),
            ("Deniz", "Kurt", "ogrenci12@ankarabilim.edu.tr", "2304003", "1230400300000", "EM-L", 1.55m, 1.55m, 5, false, 2023),
            // EEM-L: başarılı / sınırda / başarısız
            ("Baran", "Aksoy", "ogrenci13@ankarabilim.edu.tr", "2205001", "1220500100000", "EEM-L", 3.30m, 3.30m, 7, false, 2022),
            ("Selin", "Dogan", "ogrenci14@ankarabilim.edu.tr", "2405002", "1240500200000", "EEM-L", 1.95m, 1.95m, 3, false, 2024),
            ("Ege", "Koç", "ogrenci15@ankarabilim.edu.tr", "2305003", "1230500300000", "EEM-L", 1.60m, 1.60m, 5, false, 2023),
            // YM-L: başarılı / sınırda / başarısız
            ("Yusuf", "Aydin", "ogrenci16@ankarabilim.edu.tr", "2306001", "1230600100000", "YM-L", 2.70m, 2.70m, 5, false, 2023),
            ("Irem", "Tas", "ogrenci17@ankarabilim.edu.tr", "2506002", "1250600200000", "YM-L", 1.85m, 1.85m, 1, false, 2025),
            ("Can", "Polat", "ogrenci18@ankarabilim.edu.tr", "2406003", "1240600300000", "YM-L", 1.50m, 1.50m, 3, false, 2024),
        };

        var programs = context.AcademicPrograms.Include(p => p.Department).ToDictionary(p => p.Code);
        var curricula = context.Curricula.ToDictionary(c => c.ProgramId);

        foreach (var s in studentData)
        {
            var user = await EnsureUserAsync(userManager, s.Email, s.First, s.Last, "Ogrenci123!", "Student");
            if (!context.Students.Any(st => st.StudentNumber == s.Num))
            {
                var prog = programs[s.ProgCode];
                var advisorDeptId = prog.DepartmentId;
                var advisor = context.Instructors.FirstOrDefault(i => i.DepartmentId == advisorDeptId);

                var student = new Student
                {
                    UserId = user.Id,
                    StudentNumber = s.Num,
                    NationalId = s.NatId,
                    Status = StudentStatus.Active,
                    Cgpa = s.Cgpa,
                    Sgpa = s.Sgpa,
                    CompletedSemesters = s.Semesters,
                    FrozenSemesterCount = 0,
                    WithdrawalCount = 0,
                    IsScholarshipStudent = s.Scholar,
                    HasFinancialBlock = false,
                    AdvisorId = advisor?.Id
                };
                context.Students.Add(student);
                await context.SaveChangesAsync();

                // StudentProgram
                var curr = curricula[prog.Id];
                context.Set<StudentProgram>().Add(new StudentProgram
                {
                    StudentId = student.Id,
                    ProgramId = prog.Id,
                    CurriculumId = curr.Id,
                    Type = StudentProgramType.Main,
                    EnrollmentDate = new DateTime(s.Year, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                });
                await context.SaveChangesAsync();
            }
        }
    }

    private static async Task<ApplicationUser> EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email, string firstName, string lastName,
        string password, string role)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = firstName,
                LastName = lastName,
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create user '{email}': {string.Join(", ", result.Errors.Select(e => e.Description))}");
            await userManager.AddToRoleAsync(user, role);
        }
        return user;
    }
}
