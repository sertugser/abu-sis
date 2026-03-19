using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SIS.Domain.Entities;
using SIS.Domain.Enums;

namespace SIS.Infrastructure.Persistence;

public static class SeedData
{
    public static async Task SeedAsync(
        AppDbContext context,
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole<int>> roleManager)
    {
        // ============================================================
        // 1. ROLES
        // ============================================================
        string[] roles = { "Student", "Instructor", "AcademicAdvisor",
                           "AdministrativeStaff", "DepartmentManager", "SystemAdministrator" };
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                var roleResult = await roleManager.CreateAsync(new IdentityRole<int> { Name = role });
                if (!roleResult.Succeeded)
                    throw new InvalidOperationException(
                        $"Failed to create role '{role}': {string.Join(", ", roleResult.Errors.Select(e => e.Description))}");
            }
        }

        // ============================================================
        // 2. ADMIN USER
        // ============================================================
        if (await userManager.FindByEmailAsync("admin@ankarabilim.edu.tr") == null)
        {
            var admin = new ApplicationUser
            {
                UserName = "admin@ankarabilim.edu.tr",
                Email = "admin@ankarabilim.edu.tr",
                FirstName = "System",
                LastName = "Admin",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(admin, "Admin123!");
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create admin user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            await userManager.AddToRoleAsync(admin, "SystemAdministrator");
        }

        // ============================================================
        // 3. INSTRUCTOR USER + ENTITY
        // ============================================================
        ApplicationUser? instructorUser = await userManager.FindByEmailAsync("ogretmen@ankarabilim.edu.tr");
        if (instructorUser == null)
        {
            instructorUser = new ApplicationUser
            {
                UserName = "ogretmen@ankarabilim.edu.tr",
                Email = "ogretmen@ankarabilim.edu.tr",
                FirstName = "Mehmet",
                LastName = "Öztürk",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(instructorUser, "Ogretmen123!");
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create instructor user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            await userManager.AddToRoleAsync(instructorUser, "Instructor");
        }

        // ============================================================
        // 4. FACULTY + DEPARTMENT + PROGRAM
        // ============================================================
        Faculty faculty;
        if (!context.Faculties.Any())
        {
            faculty = new Faculty { Name = "Mühendislik Fakültesi", Code = "MUH" };
            context.Faculties.Add(faculty);
            await context.SaveChangesAsync();
        }
        else
        {
            faculty = context.Faculties.First();
        }

        Department dept;
        if (!context.Departments.Any())
        {
            dept = new Department { Name = "Bilgisayar Mühendisliği", Code = "BM", FacultyId = faculty.Id };
            context.Departments.Add(dept);
            await context.SaveChangesAsync();
        }
        else
        {
            dept = context.Departments.First();
        }

        AcademicProgram program;
        if (!context.AcademicPrograms.Any())
        {
            program = new AcademicProgram
            {
                Name = "Bilgisayar Mühendisliği Lisans",
                Code = "BM-L",
                DepartmentId = dept.Id,
                DegreeLevel = DegreeLevel.Bachelor,
                EducationType = EducationType.Semester,
                MaxStudyDurationSemesters = 14,
                TotalRequiredEcts = 240
            };
            context.AcademicPrograms.Add(program);
            await context.SaveChangesAsync();
        }
        else
        {
            program = context.AcademicPrograms.First();
        }

        // ============================================================
        // 3b. INSTRUCTOR ENTITY (needs department to exist first)
        // ============================================================
        Instructor instructor;
        if (!context.Instructors.Any())
        {
            instructor = new Instructor
            {
                UserId = instructorUser.Id,
                DepartmentId = dept.Id,
                Title = "Dr.",
                MaxWeeklyHours = 20
            };
            context.Instructors.Add(instructor);
            await context.SaveChangesAsync();
        }
        else
        {
            instructor = context.Instructors.First();
        }

        // ============================================================
        // 5. ACADEMIC TERMS
        //    - Active term: 2025-2026 Güz (current)
        //    - Past terms: 2024-2025 Güz (sem 1), 2024-2025 Bahar (sem 2)
        // ============================================================
        AcademicTerm activeTerm;
        AcademicTerm pastTermFall;
        AcademicTerm pastTermSpring;

        if (!context.AcademicTerms.Any())
        {
            // Past term: Semester 1 (Fall 2024-2025)
            pastTermFall = new AcademicTerm
            {
                Name = "2024-2025 Güz",
                Year = 2024,
                Semester = SemesterType.Fall,
                RegistrationStart = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                RegistrationEnd = new DateTime(2024, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                AddDropStart = new DateTime(2024, 9, 16, 0, 0, 0, DateTimeKind.Utc),
                AddDropEnd = new DateTime(2024, 9, 30, 0, 0, 0, DateTimeKind.Utc),
                WithdrawalDeadlineWeek = 7,
                TermStart = new DateTime(2024, 9, 16, 0, 0, 0, DateTimeKind.Utc),
                TermEnd = new DateTime(2025, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                IsActive = false
            };
            context.AcademicTerms.Add(pastTermFall);

            // Past term: Semester 2 (Spring 2024-2025)
            pastTermSpring = new AcademicTerm
            {
                Name = "2024-2025 Bahar",
                Year = 2024,
                Semester = SemesterType.Spring,
                RegistrationStart = new DateTime(2025, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                RegistrationEnd = new DateTime(2025, 2, 15, 0, 0, 0, DateTimeKind.Utc),
                AddDropStart = new DateTime(2025, 2, 16, 0, 0, 0, DateTimeKind.Utc),
                AddDropEnd = new DateTime(2025, 2, 28, 0, 0, 0, DateTimeKind.Utc),
                WithdrawalDeadlineWeek = 7,
                TermStart = new DateTime(2025, 2, 16, 0, 0, 0, DateTimeKind.Utc),
                TermEnd = new DateTime(2025, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                IsActive = false
            };
            context.AcademicTerms.Add(pastTermSpring);

            // Active term: 2025-2026 Güz (current — registration open, today is 2026-03-17)
            activeTerm = new AcademicTerm
            {
                Name = "2025-2026 Güz",
                Year = 2025,
                Semester = SemesterType.Fall,
                RegistrationStart = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                RegistrationEnd = new DateTime(2026, 4, 15, 0, 0, 0, DateTimeKind.Utc),
                AddDropStart = new DateTime(2026, 4, 16, 0, 0, 0, DateTimeKind.Utc),
                AddDropEnd = new DateTime(2026, 4, 30, 0, 0, 0, DateTimeKind.Utc),
                WithdrawalDeadlineWeek = 7,
                TermStart = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
                TermEnd = new DateTime(2026, 7, 15, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            };
            context.AcademicTerms.Add(activeTerm);

            await context.SaveChangesAsync();
        }
        else
        {
            activeTerm = context.AcademicTerms.First(t => t.IsActive);
            pastTermFall = context.AcademicTerms.First(t => t.Name == "2024-2025 Güz");
            pastTermSpring = context.AcademicTerms.First(t => t.Name == "2024-2025 Bahar");
        }

        // ============================================================
        // 6. COURSES — Full Computer Engineering (English) Curriculum
        // ============================================================
        if (!context.Courses.Any())
        {
            // ── Semester 1 (9 courses) ──────────────────────────────
            var ceng101 = new Course { Code = "CENG101", Name = "Algorithms and Programming with Java I", TheoryHours = 3, LabHours = 2, Ects = 6, IsNonGpa = false, DepartmentId = dept.Id };
            var ohs101  = new Course { Code = "OHS101",  Name = "Occupational Health and Safety I",       TheoryHours = 1, LabHours = 0, Ects = 1, IsNonGpa = true,  DepartmentId = dept.Id };
            var eng101  = new Course { Code = "ENG101",  Name = "Academic English I",                     TheoryHours = 2, LabHours = 0, Ects = 2, IsNonGpa = true,  DepartmentId = dept.Id };
            var math101 = new Course { Code = "MATH101", Name = "Calculus I",                             TheoryHours = 4, LabHours = 0, Ects = 6, IsNonGpa = false, DepartmentId = dept.Id };
            var phy101  = new Course { Code = "PHY101",  Name = "Physics I",                              TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var phy103  = new Course { Code = "PHY103",  Name = "Physics I Lab",                          TheoryHours = 0, LabHours = 2, Ects = 1, IsNonGpa = false, DepartmentId = dept.Id };
            var bio101  = new Course { Code = "BIO101",  Name = "Molecular Biology and Biochemistry",     TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var his101  = new Course { Code = "HIS101",  Name = "Principles of Ataturk and History of Revolutions I", TheoryHours = 2, LabHours = 0, Ects = 2, IsNonGpa = true, DepartmentId = dept.Id };
            var tur101  = new Course { Code = "TUR101",  Name = "Turkish I",                              TheoryHours = 2, LabHours = 0, Ects = 2, IsNonGpa = true,  DepartmentId = dept.Id };

            // ── Semester 2 (8 courses) ──────────────────────────────
            var ceng102 = new Course { Code = "CENG102", Name = "Algorithms and Programming with Java II", TheoryHours = 3, LabHours = 4, Ects = 8, IsNonGpa = false, DepartmentId = dept.Id };
            var ohs102  = new Course { Code = "OHS102",  Name = "Occupational Health and Safety II",       TheoryHours = 1, LabHours = 0, Ects = 1, IsNonGpa = true,  DepartmentId = dept.Id };
            var eng102  = new Course { Code = "ENG102",  Name = "Academic English II",                     TheoryHours = 2, LabHours = 0, Ects = 2, IsNonGpa = true,  DepartmentId = dept.Id };
            var math102 = new Course { Code = "MATH102", Name = "Calculus II",                             TheoryHours = 4, LabHours = 0, Ects = 6, IsNonGpa = false, DepartmentId = dept.Id };
            var phy102  = new Course { Code = "PHY102",  Name = "Physics II",                              TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var phy104  = new Course { Code = "PHY104",  Name = "Physics II Lab",                          TheoryHours = 0, LabHours = 2, Ects = 1, IsNonGpa = false, DepartmentId = dept.Id };
            var his102  = new Course { Code = "HIS102",  Name = "Principles of Ataturk and History of Revolutions II", TheoryHours = 2, LabHours = 0, Ects = 2, IsNonGpa = true, DepartmentId = dept.Id };
            var tur102  = new Course { Code = "TUR102",  Name = "Turkish II",                              TheoryHours = 2, LabHours = 0, Ects = 2, IsNonGpa = true,  DepartmentId = dept.Id };

            // ── Semester 3 (6 courses — active term) ────────────────
            var eee203  = new Course { Code = "EEE203",  Name = "Digital Design",                          TheoryHours = 3, LabHours = 2, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var eee281  = new Course { Code = "EEE281",  Name = "Electrical Circuits for Comp. Eng.",      TheoryHours = 3, LabHours = 2, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var ceng213 = new Course { Code = "CENG213", Name = "Data Structures and Algorithms",          TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var ceng223 = new Course { Code = "CENG223", Name = "Discrete and Combinatorial Math.",        TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var ceng241 = new Course { Code = "CENG241", Name = "Programming Languages",                   TheoryHours = 3, LabHours = 0, Ects = 6, IsNonGpa = false, DepartmentId = dept.Id };
            var eng201  = new Course { Code = "ENG201",  Name = "Academic English III",                    TheoryHours = 2, LabHours = 0, Ects = 2, IsNonGpa = true,  DepartmentId = dept.Id };

            // ── Semester 4 (6 courses) ─────────────────────────────────
            var ceng214 = new Course { Code = "CENG214", Name = "Algorithms II",                           TheoryHours = 3, LabHours = 0, Ects = 6, IsNonGpa = false, DepartmentId = dept.Id };
            var ceng256 = new Course { Code = "CENG256", Name = "Formal Languages and Automata Theory",    TheoryHours = 3, LabHours = 0, Ects = 6, IsNonGpa = false, DepartmentId = dept.Id };
            var math224 = new Course { Code = "MATH224", Name = "Linear Algebra and Differential Equations", TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var math206 = new Course { Code = "MATH206", Name = "Probability Theory and Statistics",       TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var seng264 = new Course { Code = "SENG264", Name = "Intro. to AI and Machine Learning",      TheoryHours = 3, LabHours = 0, Ects = 6, IsNonGpa = false, DepartmentId = dept.Id };
            var eng202  = new Course { Code = "ENG202",  Name = "Academic English IV",                     TheoryHours = 2, LabHours = 0, Ects = 2, IsNonGpa = true,  DepartmentId = dept.Id };

            // ── Semester 5 (5 zorunlu + 1 elective) ───────────────────
            var ceng301 = new Course { Code = "CENG301", Name = "Operating Systems",                       TheoryHours = 3, LabHours = 0, Ects = 6, IsNonGpa = false, DepartmentId = dept.Id };
            var ceng351 = new Course { Code = "CENG351", Name = "Database Management Systems",             TheoryHours = 3, LabHours = 0, Ects = 6, IsNonGpa = false, DepartmentId = dept.Id };
            var seng321 = new Course { Code = "SENG321", Name = "Software Engineering",                    TheoryHours = 3, LabHours = 0, Ects = 6, IsNonGpa = false, DepartmentId = dept.Id };
            var ceng361 = new Course { Code = "CENG361", Name = "Web Design and Programming",              TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var eng301  = new Course { Code = "ENG301",  Name = "Academic English V",                      TheoryHours = 2, LabHours = 0, Ects = 2, IsNonGpa = true,  DepartmentId = dept.Id };

            // ── Semester 6 (5 zorunlu + 1 elective) ───────────────────
            var ceng332 = new Course { Code = "CENG332", Name = "Computer Architecture",                   TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var ceng336 = new Course { Code = "CENG336", Name = "Computer Networks",                       TheoryHours = 3, LabHours = 0, Ects = 6, IsNonGpa = false, DepartmentId = dept.Id };
            var ceng384 = new Course { Code = "CENG384", Name = "Intro. to Signals Processing for Comp. Eng.", TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var seng324 = new Course { Code = "SENG324", Name = "Software Design Patterns",                TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var eng302  = new Course { Code = "ENG302",  Name = "Academic English VI",                     TheoryHours = 2, LabHours = 0, Ects = 2, IsNonGpa = true,  DepartmentId = dept.Id };

            // ── Semester 7 (2 zorunlu + 5 elective) ───────────────────
            var cce401  = new Course { Code = "CCE401",  Name = "Critical Thinking, Creativity and Entrepreneurship", TheoryHours = 2, LabHours = 0, Ects = 2, IsNonGpa = false, DepartmentId = dept.Id };
            var ceng491 = new Course { Code = "CENG491", Name = "Graduation Project I",                    TheoryHours = 3, LabHours = 2, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };

            // ── Semester 8 (2 zorunlu) ────────────────────────────────
            var ceng490 = new Course { Code = "CENG490", Name = "Long Term Practice",                     TheoryHours = 0, LabHours = 0, Ects = 25, IsNonGpa = false, DepartmentId = dept.Id };
            var ceng492 = new Course { Code = "CENG492", Name = "Graduation Project II",                   TheoryHours = 3, LabHours = 2, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };

            // ── Elective slots (no department — cross-departmental) ───
            var ocI     = new Course { Code = "OC-I",    Name = "Open Course I",                           TheoryHours = 0, LabHours = 0, Ects = 0, IsNonGpa = false };
            var ocII    = new Course { Code = "OC-II",   Name = "Open Course II",                          TheoryHours = 0, LabHours = 0, Ects = 0, IsNonGpa = false };
            var ndeI    = new Course { Code = "NDE-I",   Name = "Non-Departmental Elective I",             TheoryHours = 3, LabHours = 0, Ects = 3, IsNonGpa = false };
            var ndeII   = new Course { Code = "NDE-II",  Name = "Non-Departmental Elective II",            TheoryHours = 3, LabHours = 0, Ects = 3, IsNonGpa = false };
            var ndeIII  = new Course { Code = "NDE-III", Name = "Non-Departmental Elective III",           TheoryHours = 3, LabHours = 0, Ects = 3, IsNonGpa = false };
            var teI     = new Course { Code = "TE-I",    Name = "Technical Elective I",                    TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var teII    = new Course { Code = "TE-II",   Name = "Technical Elective II",                   TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var teIII   = new Course { Code = "TE-III",  Name = "Technical Elective III",                  TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var teIV    = new Course { Code = "TE-IV",   Name = "Technical Elective IV",                   TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var teV     = new Course { Code = "TE-V",    Name = "Technical Elective V",                    TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var teVI    = new Course { Code = "TE-VI",   Name = "Technical Elective VI",                   TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };
            var teVII   = new Course { Code = "TE-VII",  Name = "Technical Elective VII",                  TheoryHours = 3, LabHours = 0, Ects = 5, IsNonGpa = false, DepartmentId = dept.Id };

            context.Courses.AddRange(
                // Semester 1
                ceng101, ohs101, eng101, math101, phy101, phy103, bio101, his101, tur101,
                // Semester 2
                ceng102, ohs102, eng102, math102, phy102, phy104, his102, tur102,
                // Semester 3
                eee203, eee281, ceng213, ceng223, ceng241, eng201,
                // Semester 4
                ceng214, ceng256, math224, math206, seng264, eng202,
                // Semester 5
                ceng301, ceng351, seng321, ceng361, eng301,
                // Semester 6
                ceng332, ceng336, ceng384, seng324, eng302,
                // Semester 7
                cce401, ceng491,
                // Semester 8
                ceng490, ceng492,
                // Elective slots
                ocI, ocII, ndeI, ndeII, ndeIII, teI, teII, teIII, teIV, teV, teVI, teVII
            );
            await context.SaveChangesAsync();

            // ========================================================
            // 7. CURRICULUM + CURRICULUM COURSES
            // ========================================================
            var curriculum = new Curriculum
            {
                ProgramId = program.Id,
                Version = "2024",
                IsActive = true
            };
            context.Curricula.Add(curriculum);
            await context.SaveChangesAsync();

            // Semester 1 (9 zorunlu + 1 seçmeli)
            context.CurriculumCourses.AddRange(
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng101.Id, SuggestedSemester = 1, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ohs101.Id,  SuggestedSemester = 1, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = eng101.Id,  SuggestedSemester = 1, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = math101.Id, SuggestedSemester = 1, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = phy101.Id,  SuggestedSemester = 1, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = phy103.Id,  SuggestedSemester = 1, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = bio101.Id,  SuggestedSemester = 1, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = his101.Id,  SuggestedSemester = 1, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = tur101.Id,  SuggestedSemester = 1, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ocI.Id,     SuggestedSemester = 1, IsCompulsory = false }
            );

            // Semester 2 (8 zorunlu + 2 seçmeli)
            context.CurriculumCourses.AddRange(
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng102.Id, SuggestedSemester = 2, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ohs102.Id,  SuggestedSemester = 2, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = eng102.Id,  SuggestedSemester = 2, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = math102.Id, SuggestedSemester = 2, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = phy102.Id,  SuggestedSemester = 2, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = phy104.Id,  SuggestedSemester = 2, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = his102.Id,  SuggestedSemester = 2, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = tur102.Id,  SuggestedSemester = 2, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ocII.Id,    SuggestedSemester = 2, IsCompulsory = false },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ndeI.Id,    SuggestedSemester = 2, IsCompulsory = false }
            );

            // Semester 3 (6 zorunlu + 1 seçmeli)
            context.CurriculumCourses.AddRange(
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = eee203.Id,  SuggestedSemester = 3, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = eee281.Id,  SuggestedSemester = 3, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng213.Id, SuggestedSemester = 3, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng223.Id, SuggestedSemester = 3, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng241.Id, SuggestedSemester = 3, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = eng201.Id,  SuggestedSemester = 3, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ndeII.Id,   SuggestedSemester = 3, IsCompulsory = false }
            );

            // Semester 4 (6 zorunlu)
            context.CurriculumCourses.AddRange(
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng214.Id, SuggestedSemester = 4, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng256.Id, SuggestedSemester = 4, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = math224.Id, SuggestedSemester = 4, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = math206.Id, SuggestedSemester = 4, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = seng264.Id, SuggestedSemester = 4, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = eng202.Id,  SuggestedSemester = 4, IsCompulsory = true }
            );

            // Semester 5 (5 zorunlu + 1 seçmeli)
            context.CurriculumCourses.AddRange(
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng301.Id, SuggestedSemester = 5, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng351.Id, SuggestedSemester = 5, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = seng321.Id, SuggestedSemester = 5, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng361.Id, SuggestedSemester = 5, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = eng301.Id,  SuggestedSemester = 5, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = teI.Id,     SuggestedSemester = 5, IsCompulsory = false }
            );

            // Semester 6 (5 zorunlu + 1 seçmeli)
            context.CurriculumCourses.AddRange(
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng332.Id, SuggestedSemester = 6, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng336.Id, SuggestedSemester = 6, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng384.Id, SuggestedSemester = 6, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = seng324.Id, SuggestedSemester = 6, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = eng302.Id,  SuggestedSemester = 6, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = teII.Id,    SuggestedSemester = 6, IsCompulsory = false }
            );

            // Semester 7 (2 zorunlu + 5 seçmeli)
            context.CurriculumCourses.AddRange(
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = cce401.Id,  SuggestedSemester = 7, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng491.Id, SuggestedSemester = 7, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = teIII.Id,   SuggestedSemester = 7, IsCompulsory = false },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = teIV.Id,    SuggestedSemester = 7, IsCompulsory = false },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = teV.Id,     SuggestedSemester = 7, IsCompulsory = false },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = teVI.Id,    SuggestedSemester = 7, IsCompulsory = false },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ndeIII.Id,  SuggestedSemester = 7, IsCompulsory = false }
            );

            // Semester 8 (2 zorunlu)
            context.CurriculumCourses.AddRange(
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng490.Id, SuggestedSemester = 8, IsCompulsory = true },
                new CurriculumCourse { CurriculumId = curriculum.Id, CourseId = ceng492.Id, SuggestedSemester = 8, IsCompulsory = true }
            );

            await context.SaveChangesAsync();

            // ========================================================
            // 8. PREREQUISITES
            // ========================================================
            context.CoursePrerequisites.AddRange(
                // CENG102 requires CENG101
                new CoursePrerequisite { CourseId = ceng102.Id, RequiredCourseId = ceng101.Id },
                // MATH102 requires MATH101
                new CoursePrerequisite { CourseId = math102.Id, RequiredCourseId = math101.Id },
                // CENG213 requires CENG101
                new CoursePrerequisite { CourseId = ceng213.Id, RequiredCourseId = ceng101.Id },
                // MATH224 requires MATH101
                new CoursePrerequisite { CourseId = math224.Id, RequiredCourseId = math101.Id },
                // MATH206 requires MATH101
                new CoursePrerequisite { CourseId = math206.Id, RequiredCourseId = math101.Id },
                // SENG264 requires CENG101 AND MATH101 (two prerequisites)
                new CoursePrerequisite { CourseId = seng264.Id, RequiredCourseId = ceng101.Id },
                new CoursePrerequisite { CourseId = seng264.Id, RequiredCourseId = math101.Id },
                // CENG491 requires 8 prerequisites
                new CoursePrerequisite { CourseId = ceng491.Id, RequiredCourseId = math101.Id },
                new CoursePrerequisite { CourseId = ceng491.Id, RequiredCourseId = math102.Id },
                new CoursePrerequisite { CourseId = ceng491.Id, RequiredCourseId = phy101.Id },
                new CoursePrerequisite { CourseId = ceng491.Id, RequiredCourseId = phy102.Id },
                new CoursePrerequisite { CourseId = ceng491.Id, RequiredCourseId = phy103.Id },
                new CoursePrerequisite { CourseId = ceng491.Id, RequiredCourseId = phy104.Id },
                new CoursePrerequisite { CourseId = ceng491.Id, RequiredCourseId = ceng101.Id },
                new CoursePrerequisite { CourseId = ceng491.Id, RequiredCourseId = ceng102.Id },
                // CENG492 requires CENG491
                new CoursePrerequisite { CourseId = ceng492.Id, RequiredCourseId = ceng491.Id }
            );
            await context.SaveChangesAsync();

            // ========================================================
            // 9. CLASSROOMS
            // ========================================================
            Classroom classA201, classBLab1, classC102;
            if (!context.Classrooms.Any())
            {
                classA201  = new Classroom { Name = "A-201",  Capacity = 60, Type = ClassroomType.Lecture };
                classBLab1 = new Classroom { Name = "B-Lab1", Capacity = 30, Type = ClassroomType.Lab };
                classC102  = new Classroom { Name = "C-102",  Capacity = 40, Type = ClassroomType.Lecture };
                context.Classrooms.AddRange(classA201, classBLab1, classC102);
                await context.SaveChangesAsync();
            }
            else
            {
                classA201  = context.Classrooms.First(c => c.Name == "A-201");
                classBLab1 = context.Classrooms.First(c => c.Name == "B-Lab1");
                classC102  = context.Classrooms.First(c => c.Name == "C-102");
            }

            // ========================================================
            // 10. COURSE SECTIONS — 3rd semester (active term)
            // ========================================================
            if (!context.CourseSections.Any(cs => cs.TermId == activeTerm.Id))
            {
                context.CourseSections.AddRange(
                    // EEE203 Sek.1
                    new CourseSection
                    {
                        CourseId = eee203.Id, TermId = activeTerm.Id, InstructorId = instructor.Id,
                        Capacity = 40, EnrolledCount = 0, ClassroomId = classA201.Id, IsActive = true,
                        ScheduleJson = "[{\"Day\":1,\"StartTime\":\"09:00:00\",\"EndTime\":\"12:00:00\"},{\"Day\":3,\"StartTime\":\"13:00:00\",\"EndTime\":\"15:00:00\"}]"
                    },
                    // EEE281 Sek.1
                    new CourseSection
                    {
                        CourseId = eee281.Id, TermId = activeTerm.Id, InstructorId = instructor.Id,
                        Capacity = 40, EnrolledCount = 0, ClassroomId = classA201.Id, IsActive = true,
                        ScheduleJson = "[{\"Day\":2,\"StartTime\":\"09:00:00\",\"EndTime\":\"12:00:00\"},{\"Day\":4,\"StartTime\":\"13:00:00\",\"EndTime\":\"15:00:00\"}]"
                    },
                    // CENG213 Sek.1
                    new CourseSection
                    {
                        CourseId = ceng213.Id, TermId = activeTerm.Id, InstructorId = instructor.Id,
                        Capacity = 40, EnrolledCount = 0, ClassroomId = classC102.Id, IsActive = true,
                        ScheduleJson = "[{\"Day\":1,\"StartTime\":\"13:00:00\",\"EndTime\":\"16:00:00\"}]"
                    },
                    // CENG213 Sek.2
                    new CourseSection
                    {
                        CourseId = ceng213.Id, TermId = activeTerm.Id, InstructorId = instructor.Id,
                        Capacity = 40, EnrolledCount = 0, ClassroomId = classC102.Id, IsActive = true,
                        ScheduleJson = "[{\"Day\":3,\"StartTime\":\"09:00:00\",\"EndTime\":\"12:00:00\"}]"
                    },
                    // CENG223 Sek.1
                    new CourseSection
                    {
                        CourseId = ceng223.Id, TermId = activeTerm.Id, InstructorId = instructor.Id,
                        Capacity = 40, EnrolledCount = 0, ClassroomId = classC102.Id, IsActive = true,
                        ScheduleJson = "[{\"Day\":2,\"StartTime\":\"13:00:00\",\"EndTime\":\"16:00:00\"}]"
                    },
                    // CENG241 Sek.1
                    new CourseSection
                    {
                        CourseId = ceng241.Id, TermId = activeTerm.Id, InstructorId = instructor.Id,
                        Capacity = 40, EnrolledCount = 0, ClassroomId = classA201.Id, IsActive = true,
                        ScheduleJson = "[{\"Day\":4,\"StartTime\":\"09:00:00\",\"EndTime\":\"12:00:00\"}]"
                    },
                    // ENG201 Sek.1
                    new CourseSection
                    {
                        CourseId = eng201.Id, TermId = activeTerm.Id, InstructorId = instructor.Id,
                        Capacity = 30, EnrolledCount = 0, ClassroomId = classC102.Id, IsActive = true,
                        ScheduleJson = "[{\"Day\":5,\"StartTime\":\"10:00:00\",\"EndTime\":\"12:00:00\"}]"
                    }
                );
                await context.SaveChangesAsync();
            }

            // ========================================================
            // 11. STUDENT USER + ENTITY
            // ========================================================
            ApplicationUser? studentUser = await userManager.FindByEmailAsync("ogrenci@ankarabilim.edu.tr");
            if (studentUser == null)
            {
                studentUser = new ApplicationUser
                {
                    UserName = "ogrenci@ankarabilim.edu.tr",
                    Email = "ogrenci@ankarabilim.edu.tr",
                    FirstName = "Ayşe",
                    LastName = "Yılmaz",
                    EmailConfirmed = true
                };
                var result = await userManager.CreateAsync(studentUser, "Ogrenci123!");
                if (!result.Succeeded)
                    throw new InvalidOperationException(
                        $"Failed to create student user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
                await userManager.AddToRoleAsync(studentUser, "Student");
            }

            Student student;
            if (!context.Students.Any())
            {
                student = new Student
                {
                    UserId = studentUser.Id,
                    StudentNumber = "2101001",
                    NationalId = "12345678901",
                    Status = StudentStatus.Active,
                    Cgpa = 2.85m,
                    Sgpa = 3.00m,
                    CompletedSemesters = 2,
                    FrozenSemesterCount = 0,
                    WithdrawalCount = 0,
                    IsScholarshipStudent = true,
                    HasFinancialBlock = false,
                    AdvisorId = instructor.Id
                };
                context.Students.Add(student);
                await context.SaveChangesAsync();
            }
            else
            {
                student = context.Students.First();
            }

            // ========================================================
            // 12. STUDENT PROGRAM
            // ========================================================
            if (!context.StudentPrograms.Any())
            {
                context.StudentPrograms.Add(new StudentProgram
                {
                    StudentId = student.Id,
                    ProgramId = program.Id,
                    CurriculumId = curriculum.Id,
                    Type = StudentProgramType.Main,
                    EnrollmentDate = new DateTime(2024, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                    IsActive = true
                });
                await context.SaveChangesAsync();
            }

            // ========================================================
            // 13. PAST REGISTRATIONS — Semesters 1 & 2
            //     Creates past sections + course registrations with BB grade
            // ========================================================
            if (!context.CourseRegistrations.Any())
            {
                // Semester 1 courses and their past sections
                var semester1Courses = new[] { ceng101, ohs101, eng101, math101, phy101, phy103, bio101, his101, tur101 };
                foreach (var course in semester1Courses)
                {
                    var pastSection = new CourseSection
                    {
                        CourseId = course.Id,
                        TermId = pastTermFall.Id,
                        InstructorId = instructor.Id,
                        Capacity = 40,
                        EnrolledCount = 1,
                        IsActive = false,
                        ScheduleJson = "[]"
                    };
                    context.CourseSections.Add(pastSection);
                    await context.SaveChangesAsync();

                    context.CourseRegistrations.Add(new CourseRegistration
                    {
                        StudentId = student.Id,
                        CourseSectionId = pastSection.Id,
                        Status = RegistrationStatus.Completed,
                        Grade = GradeCode.BB,
                        IsRepeated = false,
                        MandatoryAttendance = false,
                        CourseWithdrawalCount = 0,
                        CreatedAt = new DateTime(2024, 9, 10, 0, 0, 0, DateTimeKind.Utc)
                    });
                }
                await context.SaveChangesAsync();

                // Semester 2 courses and their past sections
                var semester2Courses = new[] { ceng102, ohs102, eng102, math102, phy102, phy104, his102, tur102 };
                foreach (var course in semester2Courses)
                {
                    var pastSection = new CourseSection
                    {
                        CourseId = course.Id,
                        TermId = pastTermSpring.Id,
                        InstructorId = instructor.Id,
                        Capacity = 40,
                        EnrolledCount = 1,
                        IsActive = false,
                        ScheduleJson = "[]"
                    };
                    context.CourseSections.Add(pastSection);
                    await context.SaveChangesAsync();

                    context.CourseRegistrations.Add(new CourseRegistration
                    {
                        StudentId = student.Id,
                        CourseSectionId = pastSection.Id,
                        Status = RegistrationStatus.Completed,
                        Grade = GradeCode.BB,
                        IsRepeated = false,
                        MandatoryAttendance = false,
                        CourseWithdrawalCount = 0,
                        CreatedAt = new DateTime(2025, 2, 10, 0, 0, 0, DateTimeKind.Utc)
                    });
                }
                await context.SaveChangesAsync();
            }
        }

        // ============================================================
        // 14. ADDITIONAL PAST TERMS (2023-2024 academic year)
        // ============================================================
        var pastTermFall2023 = context.AcademicTerms.FirstOrDefault(t => t.Name == "2023-2024 Güz");
        var pastTermSpring2024 = context.AcademicTerms.FirstOrDefault(t => t.Name == "2023-2024 Bahar");

        if (pastTermFall2023 == null)
        {
            pastTermFall2023 = new AcademicTerm
            {
                Name = "2023-2024 Güz",
                Year = 2023,
                Semester = SemesterType.Fall,
                RegistrationStart = new DateTime(2023, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                RegistrationEnd = new DateTime(2023, 9, 15, 0, 0, 0, DateTimeKind.Utc),
                AddDropStart = new DateTime(2023, 9, 16, 0, 0, 0, DateTimeKind.Utc),
                AddDropEnd = new DateTime(2023, 9, 30, 0, 0, 0, DateTimeKind.Utc),
                WithdrawalDeadlineWeek = 7,
                TermStart = new DateTime(2023, 9, 16, 0, 0, 0, DateTimeKind.Utc),
                TermEnd = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc),
                IsActive = false
            };
            context.AcademicTerms.Add(pastTermFall2023);
            await context.SaveChangesAsync();
        }

        if (pastTermSpring2024 == null)
        {
            pastTermSpring2024 = new AcademicTerm
            {
                Name = "2023-2024 Bahar",
                Year = 2023,
                Semester = SemesterType.Spring,
                RegistrationStart = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc),
                RegistrationEnd = new DateTime(2024, 2, 15, 0, 0, 0, DateTimeKind.Utc),
                AddDropStart = new DateTime(2024, 2, 16, 0, 0, 0, DateTimeKind.Utc),
                AddDropEnd = new DateTime(2024, 2, 28, 0, 0, 0, DateTimeKind.Utc),
                WithdrawalDeadlineWeek = 7,
                TermStart = new DateTime(2024, 2, 16, 0, 0, 0, DateTimeKind.Utc),
                TermEnd = new DateTime(2024, 6, 15, 0, 0, 0, DateTimeKind.Utc),
                IsActive = false
            };
            context.AcademicTerms.Add(pastTermSpring2024);
            await context.SaveChangesAsync();
        }

        // ============================================================
        // 15. SECOND STUDENT — Retained (CGPA 1.62 < 1.70)
        //     Emre Kaya: started 2023, 4 completed semesters, 3rd year
        //     2 FF courses (EEE281, CENG256), must re-take before new
        //     ECTS cap: 30 (CGPA < 2.00)
        // ============================================================
        ApplicationUser? studentUser2 = await userManager.FindByEmailAsync("ogrenci2@ankarabilim.edu.tr");
        if (studentUser2 == null)
        {
            studentUser2 = new ApplicationUser
            {
                UserName = "ogrenci2@ankarabilim.edu.tr",
                Email = "ogrenci2@ankarabilim.edu.tr",
                FirstName = "Emre",
                LastName = "Kaya",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(studentUser2, "Ogrenci123!");
            if (!result.Succeeded)
                throw new InvalidOperationException(
                    $"Failed to create student2 user: {string.Join(", ", result.Errors.Select(e => e.Description))}");
            await userManager.AddToRoleAsync(studentUser2, "Student");
        }

        var student2 = context.Students.FirstOrDefault(s => s.StudentNumber == "2201001");
        if (student2 == null)
        {
            student2 = new Student
            {
                UserId = studentUser2.Id,
                StudentNumber = "2201001",
                NationalId = "98765432109",
                Status = StudentStatus.Active,
                Cgpa = 1.62m,
                Sgpa = 1.27m,
                CompletedSemesters = 4,
                FrozenSemesterCount = 0,
                WithdrawalCount = 0,
                IsScholarshipStudent = false,
                HasFinancialBlock = false,
                AdvisorId = instructor.Id
            };
            context.Students.Add(student2);
            await context.SaveChangesAsync();
        }

        // Student 2 program enrollment
        if (!context.StudentPrograms.Any(sp => sp.StudentId == student2.Id))
        {
            var activeCurriculum = context.Curricula.First(c => c.IsActive);
            context.StudentPrograms.Add(new StudentProgram
            {
                StudentId = student2.Id,
                ProgramId = program.Id,
                CurriculumId = activeCurriculum.Id,
                Type = StudentProgramType.Main,
                EnrollmentDate = new DateTime(2023, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true
            });
            await context.SaveChangesAsync();
        }

        // Student 2 past registrations (4 semesters of declining grades)
        if (!context.CourseRegistrations.Any(r => r.StudentId == student2.Id))
        {
            Course ByCode(string code) => context.Courses.First(c => c.Code == code);

            // ── Semester 1 — 2023-2024 Güz (SGPA ≈ 2.39) ──────────
            var sem1Grades = new (string Code, GradeCode Grade)[]
            {
                ("CENG101", GradeCode.CB), ("OHS101", GradeCode.S),
                ("ENG101", GradeCode.S),   ("MATH101", GradeCode.CC),
                ("PHY101", GradeCode.CB),  ("PHY103", GradeCode.BB),
                ("BIO101", GradeCode.CB),  ("HIS101", GradeCode.S),
                ("TUR101", GradeCode.S)
            };
            foreach (var (code, grade) in sem1Grades)
            {
                var course = ByCode(code);
                var section = new CourseSection
                {
                    CourseId = course.Id, TermId = pastTermFall2023.Id, InstructorId = instructor.Id,
                    Capacity = 40, EnrolledCount = 1, IsActive = false, ScheduleJson = "[]"
                };
                context.CourseSections.Add(section);
                await context.SaveChangesAsync();
                context.CourseRegistrations.Add(new CourseRegistration
                {
                    StudentId = student2.Id, CourseSectionId = section.Id,
                    Status = RegistrationStatus.Completed, Grade = grade,
                    IsRepeated = false, MandatoryAttendance = false,
                    CourseWithdrawalCount = 0,
                    CreatedAt = new DateTime(2023, 9, 10, 0, 0, 0, DateTimeKind.Utc)
                });
            }
            await context.SaveChangesAsync();

            // ── Semester 2 — 2023-2024 Bahar (SGPA ≈ 1.63) ────────
            var sem2Grades = new (string Code, GradeCode Grade)[]
            {
                ("CENG102", GradeCode.DC), ("OHS102", GradeCode.S),
                ("ENG102", GradeCode.S),   ("MATH102", GradeCode.DC),
                ("PHY102", GradeCode.CC),  ("PHY104", GradeCode.DC),
                ("HIS102", GradeCode.S),   ("TUR102", GradeCode.S)
            };
            foreach (var (code, grade) in sem2Grades)
            {
                var course = ByCode(code);
                var section = new CourseSection
                {
                    CourseId = course.Id, TermId = pastTermSpring2024.Id, InstructorId = instructor.Id,
                    Capacity = 40, EnrolledCount = 1, IsActive = false, ScheduleJson = "[]"
                };
                context.CourseSections.Add(section);
                await context.SaveChangesAsync();
                context.CourseRegistrations.Add(new CourseRegistration
                {
                    StudentId = student2.Id, CourseSectionId = section.Id,
                    Status = RegistrationStatus.Completed, Grade = grade,
                    IsRepeated = false, MandatoryAttendance = false,
                    CourseWithdrawalCount = 0,
                    CreatedAt = new DateTime(2024, 2, 10, 0, 0, 0, DateTimeKind.Utc)
                });
            }
            await context.SaveChangesAsync();

            // ── Semester 3 — 2024-2025 Güz (SGPA ≈ 1.31) — FF: EEE281
            var sem3Grades = new (string Code, GradeCode Grade)[]
            {
                ("EEE203", GradeCode.DC),  ("EEE281", GradeCode.FF),
                ("CENG213", GradeCode.CC), ("CENG223", GradeCode.DC),
                ("CENG241", GradeCode.DC), ("ENG201", GradeCode.S)
            };
            foreach (var (code, grade) in sem3Grades)
            {
                var course = ByCode(code);
                var section = new CourseSection
                {
                    CourseId = course.Id, TermId = pastTermFall.Id, InstructorId = instructor.Id,
                    Capacity = 40, EnrolledCount = 1, IsActive = false, ScheduleJson = "[]"
                };
                context.CourseSections.Add(section);
                await context.SaveChangesAsync();
                context.CourseRegistrations.Add(new CourseRegistration
                {
                    StudentId = student2.Id, CourseSectionId = section.Id,
                    Status = RegistrationStatus.Completed, Grade = grade,
                    IsRepeated = false, MandatoryAttendance = false,
                    CourseWithdrawalCount = 0,
                    CreatedAt = new DateTime(2024, 9, 10, 0, 0, 0, DateTimeKind.Utc)
                });
            }
            await context.SaveChangesAsync();

            // ── Semester 4 — 2024-2025 Bahar (SGPA ≈ 1.27) — FF: CENG256
            var sem4Grades = new (string Code, GradeCode Grade)[]
            {
                ("CENG214", GradeCode.DC), ("CENG256", GradeCode.FF),
                ("MATH224", GradeCode.CC), ("MATH206", GradeCode.DC),
                ("SENG264", GradeCode.DC)
            };
            foreach (var (code, grade) in sem4Grades)
            {
                var course = ByCode(code);
                var section = new CourseSection
                {
                    CourseId = course.Id, TermId = pastTermSpring.Id, InstructorId = instructor.Id,
                    Capacity = 40, EnrolledCount = 1, IsActive = false, ScheduleJson = "[]"
                };
                context.CourseSections.Add(section);
                await context.SaveChangesAsync();
                context.CourseRegistrations.Add(new CourseRegistration
                {
                    StudentId = student2.Id, CourseSectionId = section.Id,
                    Status = RegistrationStatus.Completed, Grade = grade,
                    IsRepeated = false, MandatoryAttendance = false,
                    CourseWithdrawalCount = 0,
                    CreatedAt = new DateTime(2025, 2, 10, 0, 0, 0, DateTimeKind.Utc)
                });
            }
            await context.SaveChangesAsync();
        }

        // ============================================================
        // 16. ELECTIVE SLOTS FOR OTHER DEPARTMENT CURRICULA
        //     Adds OC/NDE/TE placeholder courses to BSM, BMTR, YM,
        //     EM, EEM curricula (same slots used in BM).
        // ============================================================
        var slotOcI   = context.Courses.FirstOrDefault(c => c.Code == "OC-I");
        var slotOcII  = context.Courses.FirstOrDefault(c => c.Code == "OC-II");
        var slotNdeI  = context.Courses.FirstOrDefault(c => c.Code == "NDE-I");
        var slotNdeII = context.Courses.FirstOrDefault(c => c.Code == "NDE-II");
        var slotNdeIII = context.Courses.FirstOrDefault(c => c.Code == "NDE-III");
        var slotTeI   = context.Courses.FirstOrDefault(c => c.Code == "TE-I");
        var slotTeII  = context.Courses.FirstOrDefault(c => c.Code == "TE-II");
        var slotTeIII = context.Courses.FirstOrDefault(c => c.Code == "TE-III");
        var slotTeIV  = context.Courses.FirstOrDefault(c => c.Code == "TE-IV");
        var slotTeV   = context.Courses.FirstOrDefault(c => c.Code == "TE-V");
        var slotTeVI  = context.Courses.FirstOrDefault(c => c.Code == "TE-VI");
        var slotTeVII = context.Courses.FirstOrDefault(c => c.Code == "TE-VII");

        if (slotOcI != null && slotTeVI != null) // all slots exist
        {
            // (programCode, courseId, semester) tuples per department
            var electivePatterns = new (string ProgramCode, int CourseId, int Semester)[]
            {
                // BSM — from user-provided ISE curriculum
                ("BSM-L", slotOcI.Id, 1), ("BSM-L", slotNdeI!.Id, 1),
                ("BSM-L", slotOcII!.Id, 2),
                ("BSM-L", slotNdeII!.Id, 3),
                ("BSM-L", slotTeI!.Id, 5),
                ("BSM-L", slotTeII!.Id, 6),
                ("BSM-L", slotTeIII!.Id, 7), ("BSM-L", slotTeIV!.Id, 7),
                ("BSM-L", slotTeV!.Id, 7), ("BSM-L", slotTeVI.Id, 7), ("BSM-L", slotNdeIII!.Id, 7),

                // BMTR — same pattern as BM
                ("BMTR-L", slotOcI.Id, 1),
                ("BMTR-L", slotOcII.Id, 2), ("BMTR-L", slotNdeI.Id, 2),
                ("BMTR-L", slotNdeII.Id, 3),
                ("BMTR-L", slotTeI.Id, 5),
                ("BMTR-L", slotTeII.Id, 6),
                ("BMTR-L", slotTeIII.Id, 7), ("BMTR-L", slotTeIV.Id, 7),
                ("BMTR-L", slotTeV.Id, 7), ("BMTR-L", slotTeVI.Id, 7), ("BMTR-L", slotNdeIII.Id, 7),

                // YM — same pattern as BM
                ("YM-L", slotOcI.Id, 1),
                ("YM-L", slotOcII.Id, 2), ("YM-L", slotNdeI.Id, 2),
                ("YM-L", slotNdeII.Id, 3),
                ("YM-L", slotTeI.Id, 5),
                ("YM-L", slotTeII.Id, 6),
                ("YM-L", slotTeIII.Id, 7), ("YM-L", slotTeIV.Id, 7),
                ("YM-L", slotTeV.Id, 7), ("YM-L", slotTeVI.Id, 7), ("YM-L", slotNdeIII.Id, 7),

                // EM — from user-provided IE curriculum
                ("EM-L", slotOcI.Id, 1), ("EM-L", slotOcII.Id, 2),
                ("EM-L", slotNdeI.Id, 3), ("EM-L", slotNdeII.Id, 4), ("EM-L", slotNdeIII.Id, 5),
                ("EM-L", slotTeI.Id, 6), ("EM-L", slotTeII.Id, 6), ("EM-L", slotTeIII.Id, 6),
                ("EM-L", slotTeIV.Id, 7), ("EM-L", slotTeV.Id, 7), ("EM-L", slotTeVI.Id, 7),
                ("EM-L", slotTeVII!.Id, 7),

                // EEM — AKTS gap-based placement
                ("EEM-L", slotOcI.Id, 1), ("EEM-L", slotOcII.Id, 2), ("EEM-L", slotNdeI.Id, 3),
                ("EEM-L", slotNdeII.Id, 6), ("EEM-L", slotTeI.Id, 6), ("EEM-L", slotTeII.Id, 6),
                ("EEM-L", slotNdeIII.Id, 7), ("EEM-L", slotTeIII.Id, 7), ("EEM-L", slotTeIV.Id, 7),
                ("EEM-L", slotTeV.Id, 7), ("EEM-L", slotTeVI.Id, 7),
            };

            foreach (var (programCode, courseId, semester) in electivePatterns)
            {
                var prog = context.AcademicPrograms.FirstOrDefault(p => p.Code == programCode);
                if (prog == null) continue;
                var curr = context.Curricula.FirstOrDefault(c => c.ProgramId == prog.Id && c.IsActive);
                if (curr == null) continue;

                if (!context.CurriculumCourses.Any(cc => cc.CurriculumId == curr.Id && cc.CourseId == courseId))
                {
                    context.CurriculumCourses.Add(new CurriculumCourse
                    {
                        CurriculumId = curr.Id,
                        CourseId = courseId,
                        SuggestedSemester = semester,
                        IsCompulsory = false
                    });
                }
            }
            await context.SaveChangesAsync();
        }
    }
}
