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
        await EnsureUserAsync(userManager, "idari@ankarabilim.edu.tr", "Akademik", "Personel", "Idari123!", "AdministrativeStaff");
        await EnsureUserAsync(userManager, "danisman@ankarabilim.edu.tr", "Akademik", "Danisman", "Danisman123!", "AcademicAdvisor");

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
                    MaxWeeklyHours = ResolveMaxWeeklyHours(title)
                });
            }
        }
        await context.SaveChangesAsync();

        // Department Managers (also have Instructor role from above)
        await EnsureUserAsync(userManager, "ender.sevinc@ankarabilim.edu.tr", "Ender", "Sevinç", "Ogretmen123!", "DepartmentManager");
        await EnsureUserAsync(userManager, "hakan.caglar@ankarabilim.edu.tr", "Hakan", "Çağlar", "Ogretmen123!", "DepartmentManager");
        await EnsureUserAsync(userManager, "ayhan.akbas@ankarabilim.edu.tr", "Ayhan", "Akbaş", "Ogretmen123!", "DepartmentManager");
        await EnsureUserAsync(userManager, "yavuzselim.ozdemir@ankarabilim.edu.tr", "Yavuz Selim", "Özdemir", "Ogretmen123!", "DepartmentManager");

        // Dean (also has Instructor role from above — faculty-level authority)
        var deanUser = await EnsureUserAsync(userManager, "halimhaldun.goktas@ankarabilim.edu.tr", "Halim Haldun", "Göktaş", "Ogretmen123!", "Dean");
        await EnsureDeanScopeAsync(context, deanUser);

        // ============================================================
        // 3. STUDENTS (1000 total)
        // ============================================================
        // Original 18 students — names, emails, student numbers preserved
        var existingStudents = new (string First, string Last, string Email, string Num, string NatId,
            string ProgCode, decimal Cgpa, int Semesters, bool Scholar, int Year)[]
        {
            ("Ayşe", "Yılmaz", "ogrenci1@ankarabilim.edu.tr", "2201001", "1220100100000", "BM-L", 3.20m, 7, true, 2022),
            ("Emre", "Kaya", "ogrenci2@ankarabilim.edu.tr", "2401002", "1240100200000", "BM-L", 1.85m, 3, false, 2024),
            ("Burak", "Demir", "ogrenci3@ankarabilim.edu.tr", "2501003", "1250100300000", "BM-L", 1.40m, 1, false, 2025),
            ("Zeynep", "Arslan", "ogrenci4@ankarabilim.edu.tr", "2302001", "1230200100000", "BMTR-L", 3.10m, 5, false, 2023),
            ("Kenan", "Yıldız", "ogrenci5@ankarabilim.edu.tr", "2402002", "1240200200000", "BMTR-L", 1.90m, 3, false, 2024),
            ("Elif", "Ozdemir", "ogrenci6@ankarabilim.edu.tr", "2202003", "1220200300000", "BMTR-L", 1.50m, 7, false, 2022),
            ("Sergen", "Yalcin", "ogrenci7@ankarabilim.edu.tr", "2203001", "1220300100000", "BSM-L", 3.50m, 7, false, 2022),
            ("Arthur", "Morgan", "ogrenci8@ankarabilim.edu.tr", "2303002", "1230300200000", "BSM-L", 1.80m, 5, false, 2023),
            ("Orkun", "Kokcu", "ogrenci9@ankarabilim.edu.tr", "2403003", "1240300300000", "BSM-L", 1.45m, 3, false, 2024),
            ("Ceren", "Yildiz", "ogrenci10@ankarabilim.edu.tr", "2204001", "1220400100000", "EM-L", 2.80m, 7, false, 2022),
            ("Kaan", "Celik", "ogrenci11@ankarabilim.edu.tr", "2504002", "1250400200000", "EM-L", 1.75m, 1, false, 2025),
            ("Deniz", "Kurt", "ogrenci12@ankarabilim.edu.tr", "2304003", "1230400300000", "EM-L", 1.55m, 5, false, 2023),
            ("Baran", "Aksoy", "ogrenci13@ankarabilim.edu.tr", "2205001", "1220500100000", "EEM-L", 3.30m, 7, false, 2022),
            ("Selin", "Dogan", "ogrenci14@ankarabilim.edu.tr", "2405002", "1240500200000", "EEM-L", 1.95m, 3, false, 2024),
            ("Ege", "Koç", "ogrenci15@ankarabilim.edu.tr", "2305003", "1230500300000", "EEM-L", 1.60m, 5, false, 2023),
            ("Yusuf", "Aydin", "ogrenci16@ankarabilim.edu.tr", "2306001", "1230600100000", "YM-L", 2.70m, 5, false, 2023),
            ("Irem", "Tas", "ogrenci17@ankarabilim.edu.tr", "2506002", "1250600200000", "YM-L", 1.85m, 1, false, 2025),
            ("Can", "Polat", "ogrenci18@ankarabilim.edu.tr", "2406003", "1240600300000", "YM-L", 1.50m, 3, false, 2024),
        };

        var existingByProgram = existingStudents
            .GroupBy(s => s.ProgCode)
            .ToDictionary(g => g.Key, g => g.ToList());

        var programs = context.AcademicPrograms.Include(p => p.Department).ToDictionary(p => p.Code);
        var curricula = context.Curricula
            .Where(c => c.IsActive)
            .GroupBy(c => c.ProgramId)
            .ToDictionary(g => g.Key, g => g.First());
        var instructorsByDept = context.Instructors.ToList()
            .GroupBy(i => i.DepartmentId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var programDistribution = new (string ProgCode, int Count)[]
        {
            ("BM-L", 250), ("BSM-L", 200), ("BMTR-L", 150),
            ("EM-L", 150), ("EEM-L", 130), ("YM-L", 120)
        };

        var semesterGroups = new (int Low, int High, double Ratio)[]
        {
            (1, 2, 0.30), (3, 4, 0.28), (5, 6, 0.24), (7, 8, 0.18)
        };

        var rng = new Random(42);
        int globalStudentIndex = 0;

        foreach (var (progCode, totalCount) in programDistribution)
        {
            var prog = programs[progCode];
            var curr = curricula[prog.Id];
            var deptInstructors = instructorsByDept.GetValueOrDefault(prog.DepartmentId, []);
            if (deptInstructors.Count == 0)
                deptInstructors = context.Instructors.Take(1).ToList();

            int advisorIdx = 0;
            int progSeqNo = 0;

            var existing = existingByProgram.GetValueOrDefault(progCode, []);
            int existingUsed = 0;

            // Calculate group counts with rounding adjustment
            var groupCounts = semesterGroups.Select(g => (int)Math.Round(totalCount * g.Ratio)).ToArray();
            int diff = totalCount - groupCounts.Sum();
            groupCounts[0] += diff; // adjust first group to hit exact total

            for (int gi = 0; gi < semesterGroups.Length; gi++)
            {
                var (semLow, semHigh, _) = semesterGroups[gi];
                int groupCount = groupCounts[gi];
                int halfCount = groupCount / 2;

                for (int i = 0; i < groupCount; i++)
                {
                    // 50/50 odd/even split, extra goes to even
                    int completedSemesters = i < halfCount ? semLow : semHigh;
                    int entryYear = completedSemesters switch
                    {
                        >= 7 => 2022,
                        >= 5 => 2023,
                        >= 3 => 2024,
                        _ => 2025
                    };

                    // GPA category: 60% successful, 25% borderline, 15% low
                    decimal cgpa;
                    if (i < groupCount * 0.60)
                        cgpa = 2.50m + (decimal)(rng.NextDouble() * 1.30); // 2.50-3.80
                    else if (i < groupCount * 0.85)
                        cgpa = 2.00m + (decimal)(rng.NextDouble() * 0.49); // 2.00-2.49
                    else
                        cgpa = 0.80m + (decimal)(rng.NextDouble() * 1.19); // 0.80-1.99
                    cgpa = Math.Round(cgpa, 2);

                    string firstName, lastName, email, studentNum, nationalId;
                    bool isScholar;

                    if (existingUsed < existing.Count)
                    {
                        var ex = existing[existingUsed];
                        firstName = ex.First;
                        lastName = ex.Last;
                        email = ex.Email;
                        studentNum = ex.Num;
                        nationalId = ex.NatId;
                        isScholar = ex.Scholar;
                        cgpa = ex.Cgpa; // preserve original for pattern selection
                        completedSemesters = ex.Semesters;
                        entryYear = ex.Year;
                        existingUsed++;
                    }
                    else
                    {
                        progSeqNo++;
                        bool isFemale = rng.Next(2) == 0;
                        firstName = isFemale
                            ? FemaleFirstNames[rng.Next(FemaleFirstNames.Length)]
                            : MaleFirstNames[rng.Next(MaleFirstNames.Length)];
                        lastName = Surnames[rng.Next(Surnames.Length)];
                        studentNum = $"{entryYear % 100:D2}{Array.IndexOf(programDistribution.Select(p => p.ProgCode).ToArray(), progCode):D1}{progSeqNo:D4}";
                        nationalId = $"{1000000000000 + globalStudentIndex + 18}";
                        email = $"ogrenci{globalStudentIndex + 1}@ankarabilim.edu.tr";
                        isScholar = globalStudentIndex % 10 == 0;
                    }

                    globalStudentIndex++;

                    var user = await EnsureUserAsync(userManager, email, firstName, lastName, "Ogrenci123!", "Student");
                    if (!context.Students.Any(st => st.UserId == user.Id || st.NationalId == nationalId || st.StudentNumber == studentNum))
                    {
                        var advisor = deptInstructors[advisorIdx % deptInstructors.Count];
                        advisorIdx++;

                        var student = new Student
                        {
                            UserId = user.Id,
                            StudentNumber = studentNum,
                            NationalId = nationalId,
                            Status = StudentStatus.Active,
                            Cgpa = cgpa,
                            Sgpa = cgpa,
                            CompletedSemesters = completedSemesters,
                            FrozenSemesterCount = 0,
                            WithdrawalCount = 0,
                            IsScholarshipStudent = isScholar,
                            HasFinancialBlock = false,
                            AdvisorId = advisor.Id
                        };
                        context.Students.Add(student);
                        await context.SaveChangesAsync();

                        context.Set<StudentProgram>().Add(new StudentProgram
                        {
                            StudentId = student.Id,
                            ProgramId = prog.Id,
                            CurriculumId = curr.Id,
                            Type = StudentProgramType.Main,
                            EnrollmentDate = new DateTime(entryYear, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                            IsActive = true
                        });
                        await context.SaveChangesAsync();
                    }
                }
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
        }

        if (!await userManager.IsInRoleAsync(user, role))
        {
            var addRoleResult = await userManager.AddToRoleAsync(user, role);
            if (!addRoleResult.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to assign role '{role}' to '{email}': {string.Join(", ", addRoleResult.Errors.Select(e => e.Description))}");
        }

        return user;
    }

    private static int ResolveMaxWeeklyHours(string title) => title switch
    {
        var t when t.StartsWith("Prof.") => 10,
        var t when t.StartsWith("Doç.") => 12,
        var t when t.StartsWith("Dr. Öğr.") || t.StartsWith("Dr.") => 12,
        var t when t.StartsWith("Öğr. Gör.") => 16,
        var t when t.Contains("Prof.") => 10, // "Emeritus Prof. Dr." etc.
        _ => 20 // Arş. Gör. and others
    };

    private static async Task EnsureDeanScopeAsync(AppDbContext context, ApplicationUser deanUser)
    {
        var existingScope = await context.UserScopes
            .FirstOrDefaultAsync(scope => scope.UserId == deanUser.Id && scope.Kind == SIS.Domain.Identity.ScopeKind.Faculty);
        if (existingScope is not null)
            return;

        var facultyId = await context.Instructors
            .Where(instructor => instructor.UserId == deanUser.Id)
            .Select(instructor => (int?)instructor.Department.FacultyId)
            .FirstOrDefaultAsync();
        if (facultyId is null)
            return;

        context.UserScopes.Add(new SIS.Domain.Identity.UserScope
        {
            UserId = deanUser.Id,
            Kind = SIS.Domain.Identity.ScopeKind.Faculty,
            FacultyId = facultyId.Value,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await context.SaveChangesAsync();
    }
}
