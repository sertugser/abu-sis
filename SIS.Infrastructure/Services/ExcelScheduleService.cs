using ClosedXML.Excel;
using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Domain.Entities;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class ExcelScheduleService : IExcelScheduleService
{
    private static readonly TimeSpan ImportSessionLifetime = TimeSpan.FromMinutes(30);
    private static readonly JsonSerializerOptions SessionJsonOptions = new(JsonSerializerDefaults.Web);
    private readonly AppDbContext _db;

    public ExcelScheduleService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<byte[]> ExportScheduleAsync(
        int termId,
        int facultyId,
        int? departmentId = null,
        CancellationToken ct = default)
    {
        // Load all section-schedules for the term/faculty, optionally filtered by department
        var query = _db.SectionSchedules
            .Where(ss => ss.CourseSection.TermId == termId
                      && ss.CourseSection.Course.Department!.FacultyId == facultyId)
            .AsQueryable();

        if (departmentId.HasValue)
        {
            query = query.Where(ss => ss.CourseSection.Course.DepartmentId == departmentId.Value);
        }

        var rows = await query
            .Select(ss => new
            {
                CourseCode = ss.CourseSection.Course.Code,
                CourseName = ss.CourseSection.Course.Name,
                SectionNumber = ss.CourseSection.SectionNumber,
                InstructorUserId = ss.CourseSection.Instructor != null ? ss.CourseSection.Instructor.UserId : (int?)null,
                InstructorTitle = ss.CourseSection.Instructor != null ? ss.CourseSection.Instructor.Title : string.Empty,
                DayOfWeek = ss.TimeSlot.DayOfWeek,
                StartTime = ss.TimeSlot.StartTime,
                EndTime = ss.TimeSlot.EndTime,
                SlotOrder = ss.TimeSlot.SlotOrder,
                ClassroomName = ss.Classroom!.Name,
                Capacity = ss.CourseSection.Capacity,
                IsActive = ss.CourseSection.IsActive,
                IsFinalized = ss.CourseSection.IsFinalized,
            })
            .OrderBy(r => r.CourseCode)
            .ThenBy(r => r.SectionNumber)
            .ThenBy(r => r.DayOfWeek)
            .ThenBy(r => r.SlotOrder)
            .ToListAsync(ct);

        // Collect instructor user IDs to fetch names
        var userIds = rows
            .Where(r => r.InstructorUserId.HasValue)
            .Select(r => r.InstructorUserId!.Value)
            .Distinct()
            .ToList();

        var userNames = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        using var workbook = new XLWorkbook();

        // ── Sheet 1: Program Verisi ──────────────────────────────────────────
        var dataSheet = workbook.Worksheets.Add("Program Verisi");

        var headers = new[]
        {
            "Ders Kodu", "Ders Adı", "Şube", "Öğretim Üyesi",
            "Gün", "Başlangıç", "Bitiş", "Derslik", "Kapasite", "Durum"
        };

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = dataSheet.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x1F, 0x49, 0x7D);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        int dataRow = 2;
        foreach (var row in rows)
        {
            string instructorName = row.InstructorUserId.HasValue
                ? userNames.TryGetValue(row.InstructorUserId.Value, out var name) ? name : "-"
                : "-";

            if (!string.IsNullOrWhiteSpace(row.InstructorTitle) && instructorName != "-")
                instructorName = $"{row.InstructorTitle} {instructorName}";

            string status = row.IsFinalized ? "Kesinleşti"
                          : row.IsActive    ? "Aktif"
                                            : "Pasif";

            dataSheet.Cell(dataRow, 1).Value = row.CourseCode;
            dataSheet.Cell(dataRow, 2).Value = row.CourseName;
            dataSheet.Cell(dataRow, 3).Value = row.SectionNumber;
            dataSheet.Cell(dataRow, 4).Value = instructorName;
            dataSheet.Cell(dataRow, 5).Value = DayOfWeekTurkish(row.DayOfWeek);
            dataSheet.Cell(dataRow, 6).Value = row.StartTime.ToString(@"HH\:mm");
            dataSheet.Cell(dataRow, 7).Value = row.EndTime.ToString(@"HH\:mm");
            dataSheet.Cell(dataRow, 8).Value = row.ClassroomName;
            dataSheet.Cell(dataRow, 9).Value = row.Capacity;
            dataSheet.Cell(dataRow, 10).Value = status;

            // Zebra striping
            if (dataRow % 2 == 0)
            {
                dataSheet.Row(dataRow).Style.Fill.BackgroundColor = XLColor.FromArgb(0xEF, 0xF4, 0xFF);
            }

            dataRow++;
        }

        dataSheet.Columns().AdjustToContents();
        dataSheet.SheetView.FreezeRows(1);

        // Add auto-filter on header row
        if (dataRow > 2)
        {
            dataSheet.RangeUsed()?.SetAutoFilter();
        }

        // ── Sheet 2: Haftalık Grid ───────────────────────────────────────────
        var gridSheet = workbook.Worksheets.Add("Haftalık Grid");

        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

        // Collect distinct slot orders for the term
        var slotOrders = rows
            .Select(r => (r.SlotOrder, r.StartTime, r.EndTime))
            .Distinct()
            .OrderBy(s => s.SlotOrder)
            .ToList();

        // Header row: col 1 = "Saat / Gün", cols 2-6 = days
        gridSheet.Cell(1, 1).Value = "Saat / Gün";
        gridSheet.Cell(1, 1).Style.Font.Bold = true;
        gridSheet.Cell(1, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0x1F, 0x49, 0x7D);
        gridSheet.Cell(1, 1).Style.Font.FontColor = XLColor.White;

        for (int d = 0; d < days.Length; d++)
        {
            var cell = gridSheet.Cell(1, d + 2);
            cell.Value = DayOfWeekTurkish(days[d]);
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x1F, 0x49, 0x7D);
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        }

        // Build a lookup: (slotOrder, dayOfWeek) → list of cell texts
        var gridLookup = new Dictionary<(int slotOrder, DayOfWeek day), List<string>>();

        foreach (var row in rows)
        {
            string instructorName = row.InstructorUserId.HasValue
                ? userNames.TryGetValue(row.InstructorUserId.Value, out var name) ? name : "-"
                : "-";

            string cellText = $"{row.CourseCode}-{row.SectionNumber}\n{instructorName}\n{row.ClassroomName}";
            var key = (row.SlotOrder, row.DayOfWeek);

            if (!gridLookup.ContainsKey(key))
                gridLookup[key] = new List<string>();

            gridLookup[key].Add(cellText);
        }

        // Fill slot rows
        for (int s = 0; s < slotOrders.Count; s++)
        {
            int gridRow = s + 2;
            var (slotOrder, startTime, endTime) = slotOrders[s];

            string slotLabel = $"{startTime:HH\\:mm}-{endTime:HH\\:mm}";
            var labelCell = gridSheet.Cell(gridRow, 1);
            labelCell.Value = slotLabel;
            labelCell.Style.Font.Bold = true;
            labelCell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xD6, 0xE4, 0xF7);

            for (int d = 0; d < days.Length; d++)
            {
                var key = (slotOrder, days[d]);
                var dataCell = gridSheet.Cell(gridRow, d + 2);

                if (gridLookup.TryGetValue(key, out var texts))
                {
                    dataCell.Value = string.Join("\n---\n", texts);
                    dataCell.Style.Alignment.WrapText = true;
                    dataCell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xE8, 0xF5, 0xE9);
                }

                dataCell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                dataCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
        }

        gridSheet.Columns().AdjustToContents();
        gridSheet.SheetView.FreezeRows(1);
        gridSheet.SheetView.FreezeColumns(1);

        // ── Serialize to byte array ──────────────────────────────────────────
        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public async Task<byte[]> ExportPdfAsync(
        int termId,
        int facultyId,
        int? departmentId = null,
        CancellationToken ct = default)
    {
        QuestPDF.Settings.License = LicenseType.Community;

        // ── Load metadata ────────────────────────────────────────────────────
        var term = await _db.AcademicTerms
            .Where(t => t.Id == termId)
            .Select(t => new { t.Id, t.Name })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Dönem bulunamadı: {termId}");

        var faculty = await _db.Faculties
            .Where(f => f.Id == facultyId)
            .Select(f => new { f.Id, f.Name })
            .FirstOrDefaultAsync(ct)
            ?? throw new InvalidOperationException($"Fakülte bulunamadı: {facultyId}");

        // Departments to export
        var departmentsQuery = _db.Departments.Where(d => d.FacultyId == facultyId);
        if (departmentId.HasValue)
            departmentsQuery = departmentsQuery.Where(d => d.Id == departmentId.Value);

        var departments = await departmentsQuery
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name })
            .ToListAsync(ct);

        // ── Load section-schedule data ───────────────────────────────────────
        var baseQuery = _db.SectionSchedules
            .Where(ss => ss.CourseSection.TermId == termId
                      && ss.CourseSection.Course.Department!.FacultyId == facultyId);

        if (departmentId.HasValue)
            baseQuery = baseQuery.Where(ss => ss.CourseSection.Course.DepartmentId == departmentId.Value);

        var rows = await baseQuery
            .Select(ss => new
            {
                DepartmentId   = ss.CourseSection.Course.DepartmentId,
                CourseCode     = ss.CourseSection.Course.Code,
                CourseName     = ss.CourseSection.Course.Name,
                SectionNumber  = ss.CourseSection.SectionNumber,
                Capacity       = ss.CourseSection.Capacity,
                InstructorUserId = ss.CourseSection.Instructor != null ? ss.CourseSection.Instructor.UserId : (int?)null,
                InstructorTitle  = ss.CourseSection.Instructor != null ? ss.CourseSection.Instructor.Title : string.Empty,
                DayOfWeek      = ss.TimeSlot.DayOfWeek,
                StartTime      = ss.TimeSlot.StartTime,
                EndTime        = ss.TimeSlot.EndTime,
                SlotOrder      = ss.TimeSlot.SlotOrder,
                ClassroomName  = ss.Classroom!.Name,
            })
            .OrderBy(r => r.CourseCode)
            .ThenBy(r => r.SectionNumber)
            .ThenBy(r => r.DayOfWeek)
            .ThenBy(r => r.SlotOrder)
            .ToListAsync(ct);

        // Fetch instructor names
        var userIds = rows.Where(r => r.InstructorUserId.HasValue)
                          .Select(r => r.InstructorUserId!.Value)
                          .Distinct().ToList();

        var userNames = await _db.Users
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.FirstName, u.LastName })
            .ToDictionaryAsync(u => u.Id, u => $"{u.FirstName} {u.LastName}".Trim(), ct);

        // ── Helpers ──────────────────────────────────────────────────────────
        string InstructorDisplay(int? uid, string title)
        {
            if (uid == null) return "-";
            if (!userNames.TryGetValue(uid.Value, out var name)) return "-";
            return string.IsNullOrWhiteSpace(title) ? name : $"{title} {name}";
        }

        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday };

        // ── Build PDF document ───────────────────────────────────────────────
        var document = Document.Create(container =>
        {
            bool isAllDepts = !departmentId.HasValue;

            // Cover page (all-departments mode only)
            if (isAllDepts)
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(2, Unit.Centimetre);
                    page.Content().Column(col =>
                    {
                        col.Spacing(20);
                        col.Item().AlignCenter().Text("ANKARA BİLİM ÜNİVERSİTESİ")
                            .FontSize(22).Bold();
                        col.Item().AlignCenter().Text(faculty.Name)
                            .FontSize(16).SemiBold();
                        col.Item().AlignCenter().Text($"Ders Programları — {term.Name}")
                            .FontSize(14);
                        col.Item().PaddingTop(20).Text("İçindekiler").FontSize(12).Bold();
                        foreach (var dept in departments)
                        {
                            col.Item().Text($"• {dept.Name}").FontSize(11);
                        }
                    });
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span($"Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(8);
                    });
                });
            }

            // Per-department pages
            foreach (var dept in departments)
            {
                var deptRows = rows.Where(r => r.DepartmentId == dept.Id).ToList();

                // ── Distinct time slots for this department ────────────────
                var slotOrders = deptRows
                    .Select(r => (r.SlotOrder, r.StartTime, r.EndTime))
                    .Distinct()
                    .OrderBy(s => s.SlotOrder)
                    .ToList();

                // ── Page 1: Weekly Grid ────────────────────────────────────
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);

                    page.Header().Column(hdr =>
                    {
                        hdr.Item().Text("ANKARA BİLİM ÜNİVERSİTESİ").FontSize(13).Bold().AlignCenter();
                        hdr.Item().Text(faculty.Name).FontSize(11).AlignCenter();
                        hdr.Item().Text(dept.Name).FontSize(10).AlignCenter();
                        hdr.Item().Text(term.Name).FontSize(9).AlignCenter();
                        hdr.Item().PaddingTop(4).LineHorizontal(1);
                    });

                    page.Content().PaddingTop(8).Table(table =>
                    {
                        // Columns: Saat + 5 days
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);
                            for (int i = 0; i < 5; i++) cols.RelativeColumn(3);
                        });

                        // Header row
                        table.Header(header =>
                        {
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                                .Text("Saat").FontColor(Colors.White).FontSize(8).Bold();
                            foreach (var day in days)
                            {
                                header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                                    .AlignCenter().Text(DayOfWeekTurkish(day))
                                    .FontColor(Colors.White).FontSize(8).Bold();
                            }
                        });

                        // Build grid lookup
                        var gridLookup = new Dictionary<(int slot, DayOfWeek day), List<string>>();
                        foreach (var r in deptRows)
                        {
                            string instructor = InstructorDisplay(r.InstructorUserId, r.InstructorTitle);
                            string cellText = $"{r.CourseCode}-{r.SectionNumber}\n{instructor}\n{r.ClassroomName}";
                            var key = (r.SlotOrder, r.DayOfWeek);
                            if (!gridLookup.ContainsKey(key))
                                gridLookup[key] = new List<string>();
                            gridLookup[key].Add(cellText);
                        }

                        // Data rows
                        for (int s = 0; s < slotOrders.Count; s++)
                        {
                            var (slotOrder, startTime, endTime) = slotOrders[s];
                            string slotLabel = $"{startTime:HH\\:mm}\n{endTime:HH\\:mm}";
                            bool isEven = s % 2 == 0;
                            var rowBg = isEven ? Colors.Grey.Lighten4 : Colors.White;

                            table.Cell().Background(Colors.Blue.Lighten4).Padding(4)
                                .Text(slotLabel).FontSize(7).Bold();

                            foreach (var day in days)
                            {
                                var key = (slotOrder, day);
                                var cellBg = gridLookup.ContainsKey(key) ? Colors.Green.Lighten4 : rowBg;
                                var cellText = gridLookup.TryGetValue(key, out var texts)
                                    ? string.Join("\n---\n", texts)
                                    : string.Empty;

                                table.Cell().Background(cellBg).Border(0.5f).Padding(3)
                                    .AlignCenter().Text(cellText).FontSize(7);
                            }
                        }

                        if (slotOrders.Count == 0)
                        {
                            table.Cell().ColumnSpan(6).Padding(10)
                                .AlignCenter().Text("Bu bölüm için programlanmış ders bulunmamaktadır.")
                                .FontSize(9).Italic();
                        }
                    });

                    page.Footer().Row(row =>
                    {
                        row.RelativeItem().Text($"Oluşturulma: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(7);
                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Sayfa ").FontSize(7);
                            x.CurrentPageNumber().FontSize(7);
                            x.Span(" / ").FontSize(7);
                            x.TotalPages().FontSize(7);
                        });
                    });
                });

                // ── Page 2: Course List ────────────────────────────────────
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);

                    page.Header().Column(hdr =>
                    {
                        hdr.Item().Text("ANKARA BİLİM ÜNİVERSİTESİ").FontSize(13).Bold().AlignCenter();
                        hdr.Item().Text($"{faculty.Name} — {dept.Name}").FontSize(11).AlignCenter();
                        hdr.Item().Text($"Ders Listesi — {term.Name}").FontSize(10).AlignCenter();
                        hdr.Item().PaddingTop(4).LineHorizontal(1);
                    });

                    page.Content().PaddingTop(8).Table(table =>
                    {
                        table.ColumnsDefinition(cols =>
                        {
                            cols.RelativeColumn(2);  // Ders Kodu
                            cols.RelativeColumn(5);  // Ders Adı
                            cols.RelativeColumn(1);  // Şube
                            cols.RelativeColumn(4);  // Öğretim Üyesi
                            cols.RelativeColumn(2);  // Gün
                            cols.RelativeColumn(2);  // Saat
                            cols.RelativeColumn(2);  // Derslik
                            cols.RelativeColumn(1.5f); // Kapasite
                        });

                        table.Header(header =>
                        {
                            var headers = new[] { "Ders Kodu", "Ders Adı", "Şube", "Öğretim Üyesi", "Gün", "Saat", "Derslik", "Kapasite" };
                            foreach (var h in headers)
                            {
                                header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                                    .Text(h).FontColor(Colors.White).FontSize(8).Bold();
                            }
                        });

                        int rowIdx = 0;
                        foreach (var r in deptRows)
                        {
                            var bg = rowIdx++ % 2 == 0 ? Colors.Grey.Lighten5 : Colors.White;
                            string instructor = InstructorDisplay(r.InstructorUserId, r.InstructorTitle);
                            string slotTime = $"{r.StartTime:HH\\:mm}-{r.EndTime:HH\\:mm}";

                            table.Cell().Background(bg).Border(0.5f).Padding(3).Text(r.CourseCode).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).Text(r.CourseName).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).AlignCenter().Text(r.SectionNumber.ToString()).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).Text(instructor).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).Text(DayOfWeekTurkish(r.DayOfWeek)).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).Text(slotTime).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).Text(r.ClassroomName).FontSize(8);
                            table.Cell().Background(bg).Border(0.5f).Padding(3).AlignCenter().Text(r.Capacity.ToString()).FontSize(8);
                        }

                        if (deptRows.Count == 0)
                        {
                            table.Cell().ColumnSpan(8).Padding(10)
                                .AlignCenter().Text("Listelenecek ders bulunmamaktadır.")
                                .FontSize(9).Italic();
                        }
                    });

                    page.Footer().Row(row =>
                    {
                        int uniqueCourses = deptRows.Select(r => r.CourseCode).Distinct().Count();
                        int uniqueInstructors = deptRows
                            .Where(r => r.InstructorUserId.HasValue)
                            .Select(r => r.InstructorUserId).Distinct().Count();

                        row.RelativeItem().Text($"Toplam: {uniqueCourses} ders, {uniqueInstructors} öğretim üyesi")
                            .FontSize(7);
                        row.RelativeItem().AlignRight().Text(x =>
                        {
                            x.Span("Sayfa ").FontSize(7);
                            x.CurrentPageNumber().FontSize(7);
                            x.Span(" / ").FontSize(7);
                            x.TotalPages().FontSize(7);
                        });
                    });
                });
            }
        });

        return document.GeneratePdf();
    }

    public async Task<ImportPreviewDto> PreviewImportAsync(int userId, Stream fileStream, int termId, int facultyId, CancellationToken ct = default)
    {
        // Turkish day name → DayOfWeek (normalised: lowercase, no diacritics for resilience)
        var dayMap = new Dictionary<string, DayOfWeek>(StringComparer.OrdinalIgnoreCase)
        {
            { "Pazartesi", DayOfWeek.Monday },
            { "Salı",      DayOfWeek.Tuesday },
            { "Sali",      DayOfWeek.Tuesday },
            { "Çarşamba",  DayOfWeek.Wednesday },
            { "Carsamba",  DayOfWeek.Wednesday },
            { "Perşembe",  DayOfWeek.Thursday },
            { "Persembe",  DayOfWeek.Thursday },
            { "Cuma",      DayOfWeek.Friday },
        };

        // Pre-load reference data for the term/faculty to avoid N+1 queries
        var sections = await _db.CourseSections
            .Where(s => s.TermId == termId && s.Course.Department!.FacultyId == facultyId)
            .Include(s => s.Course)
            .Include(s => s.SectionSchedules)
            .ToListAsync(ct);

        var sectionLookup = sections.ToDictionary(
            s => (s.Course.Code, s.SectionNumber),
            s => s);

        var timeSlots = await _db.TimeSlots
            .Where(ts => ts.TermId == termId)
            .ToListAsync(ct);

        var classrooms = await _db.Classrooms.ToListAsync(ct);
        var classroomLookup = classrooms
            .GroupBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        // Existing schedules (all sections in term/faculty) for conflict detection
        var existingSchedules = await _db.SectionSchedules
            .Where(ss => ss.CourseSection.TermId == termId
                      && ss.CourseSection.Course.Department!.FacultyId == facultyId)
            .Include(ss => ss.CourseSection)
                .ThenInclude(cs => cs.Instructor)
            .ToListAsync(ct);

        using var workbook = new XLWorkbook(fileStream);
        var sheet = workbook.Worksheets.FirstOrDefault(ws => ws.Name == "Program Verisi")
                    ?? workbook.Worksheet(1);

        var serverRows = new List<ScheduleImportSessionRow>();
        var importedSchedules = new List<(int SectionId, int TimeSlotId, int ClassroomId, int RowNumber)>();

        int lastRow = sheet.LastRowUsed()?.RowNumber() ?? 1;

        for (int r = 2; r <= lastRow; r++)
        {
            var row = new ScheduleImportSessionRow { RowNumber = r };

            string courseCode     = sheet.Cell(r, 1).GetValue<string>().Trim();
            string sectionCodeRaw = sheet.Cell(r, 3).GetValue<string>().Trim();
            string dayRaw         = sheet.Cell(r, 5).GetValue<string>().Trim();
            string startTimeRaw   = sheet.Cell(r, 6).GetValue<string>().Trim();
            string endTimeRaw     = sheet.Cell(r, 7).GetValue<string>().Trim();
            string classroomName  = sheet.Cell(r, 8).GetValue<string>().Trim();
            string capacityRaw    = sheet.Cell(r, 9).GetValue<string>().Trim();

            // Skip rows with no course code (likely empty)
            if (string.IsNullOrWhiteSpace(courseCode))
                continue;

            row.CourseCode    = courseCode;
            row.SectionCode   = sectionCodeRaw;
            row.Day           = dayRaw;
            row.StartTime     = startTimeRaw;
            row.EndTime       = endTimeRaw;
            row.ClassroomName = classroomName;
            _ = int.TryParse(capacityRaw, out int capacity);
            row.Capacity      = capacity;

            // Skip rows with no day/time (unassigned — no change intended)
            if (string.IsNullOrWhiteSpace(dayRaw) || string.IsNullOrWhiteSpace(startTimeRaw))
            {
                row.Status  = "Skipped";
                row.Message = "Gün veya saat bilgisi yok, atlandı.";
                serverRows.Add(row);
                continue;
            }

            var errors = new List<string>();

            // Validate: section number
            if (!int.TryParse(sectionCodeRaw, out int sectionNumber))
            {
                errors.Add($"Şube kodu geçersiz: '{sectionCodeRaw}'");
            }

            // Validate: course + section exists
            CourseSection? section = null;
            if (errors.Count == 0 && !sectionLookup.TryGetValue((courseCode, sectionNumber), out section))
            {
                errors.Add($"Şube bulunamadı: {courseCode}-{sectionNumber}");
            }

            // Validate: day name
            DayOfWeek dayOfWeek = DayOfWeek.Monday;
            if (!dayMap.TryGetValue(dayRaw, out dayOfWeek))
            {
                errors.Add($"Geçersiz gün adı: '{dayRaw}'");
            }

            // Validate: start/end time parse
            TimeOnly startTime = default, endTime = default;
            if (!TimeOnly.TryParse(startTimeRaw, out startTime))
                errors.Add($"Geçersiz başlangıç saati: '{startTimeRaw}'");
            if (!string.IsNullOrWhiteSpace(endTimeRaw) && !TimeOnly.TryParse(endTimeRaw, out endTime))
                errors.Add($"Geçersiz bitiş saati: '{endTimeRaw}'");

            // Validate: timeslot exists
            TimeSlot? timeSlot = null;
            if (errors.Count == 0)
            {
                timeSlot = timeSlots.FirstOrDefault(ts =>
                    ts.DayOfWeek == dayOfWeek &&
                    ts.StartTime == startTime);

                if (timeSlot == null)
                    errors.Add($"Zaman dilimi bulunamadı: {dayRaw} {startTimeRaw}");
            }

            // Validate: classroom exists
            Classroom? classroom = null;
            if (!string.IsNullOrWhiteSpace(classroomName) &&
                !classroomLookup.TryGetValue(classroomName, out classroom))
            {
                errors.Add($"Derslik bulunamadı: '{classroomName}'");
            }
            else if (string.IsNullOrWhiteSpace(classroomName))
            {
                errors.Add("Derslik adı boş olamaz.");
            }

            if (errors.Count > 0)
            {
                row.Status  = "Error";
                row.Message = string.Join("; ", errors);
                serverRows.Add(row);
                continue;
            }

            // At this point all lookups succeeded
            row.SectionId  = section!.Id;
            row.TimeSlotId = timeSlot!.Id;
            row.ClassroomId = classroom!.Id;

            var warnings = new List<string>();

            // Conflict check 1: classroom already used in this timeslot (existing DB schedules)
            bool classroomConflictDb = existingSchedules.Any(ss =>
                ss.ClassroomId == classroom.Id &&
                ss.TimeSlotId  == timeSlot.Id &&
                ss.CourseSection.Id != section.Id);

            // Conflict check 2: classroom used in another import row (within this import batch)
            bool classroomConflictImport = importedSchedules.Any(i =>
                i.ClassroomId == classroom.Id &&
                i.TimeSlotId  == timeSlot.Id &&
                i.SectionId   != section.Id);

            if (classroomConflictDb || classroomConflictImport)
                warnings.Add($"Derslik çakışması: {classroomName} bu saatte başka bir şubede kullanılıyor.");

            // Conflict check 3: instructor conflict
            if (section.InstructorId.HasValue)
            {
                bool instructorConflictDb = existingSchedules.Any(ss =>
                    ss.CourseSection.InstructorId == section.InstructorId &&
                    ss.TimeSlotId == timeSlot.Id &&
                    ss.CourseSection.Id != section.Id);

                bool instructorConflictImport = importedSchedules.Any(i =>
                {
                    var imp = sections.FirstOrDefault(s => s.Id == i.SectionId);
                    return imp?.InstructorId == section.InstructorId &&
                           i.TimeSlotId == timeSlot.Id &&
                           i.SectionId  != section.Id;
                });

                if (instructorConflictDb || instructorConflictImport)
                    warnings.Add("Öğretim üyesi çakışması: Hoca bu saatte başka bir şubede.");
            }

            // Capacity warning
            if (capacity > 0 && classroom.Capacity < capacity)
                warnings.Add($"Kapasite uyarısı: Derslik kapasitesi ({classroom.Capacity}) şube kapasitesinden ({capacity}) küçük.");

            if (warnings.Count > 0)
            {
                row.Status  = "Warning";
                row.Message = string.Join("; ", warnings);
            }
            else
            {
                row.Status  = "Valid";
                row.Message = "Hazır.";
            }

            importedSchedules.Add((section.Id, timeSlot.Id, classroom.Id, r));
            serverRows.Add(row);
        }

        var now = DateTime.UtcNow;
        var session = new ScheduleImportSession
        {
            Id = Guid.NewGuid(),
            CreatedByUserId = userId,
            FacultyId = facultyId,
            TermId = termId,
            RowsJson = JsonSerializer.Serialize(serverRows, SessionJsonOptions),
            CreatedAt = now,
            ExpiresAt = now.Add(ImportSessionLifetime),
        };

        _db.ScheduleImportSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        return new ImportPreviewDto
        {
            ImportSessionId = session.Id,
            ExpiresAt = session.ExpiresAt,
            TotalRows = serverRows.Count,
            Rows = serverRows.Select(ToPublicRow).ToList(),
        };
    }

    public async Task<ImportResultDto> ApplyImportAsync(
        int userId,
        int facultyId,
        int termId,
        Guid importSessionId,
        List<int> rowNumbers,
        CancellationToken ct = default)
    {
        if (importSessionId == Guid.Empty)
            throw new ScheduleImportSessionInvalidException("Import session is required.");

        var selectedRowNumbers = rowNumbers.Distinct().ToList();
        if (selectedRowNumbers.Count == 0)
            throw new ScheduleImportSessionInvalidException("At least one row must be selected.");

        IDbContextTransaction? transaction = null;
        try
        {
            if (_db.Database.IsRelational())
            {
                transaction = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            }

            var now = DateTime.UtcNow;
            var session = await _db.ScheduleImportSessions
                .FirstOrDefaultAsync(s => s.Id == importSessionId, ct)
                ?? throw new ScheduleImportSessionInvalidException("Import session was not found.");

            if (session.CreatedByUserId != userId)
                throw new ScheduleImportSessionForbiddenException("Import session belongs to another user.");

            if (session.FacultyId != facultyId || session.TermId != termId)
                throw new ScheduleImportSessionInvalidException("Import session does not match the requested faculty or term.");

            if (session.ConsumedAt.HasValue)
                throw new ScheduleImportSessionInvalidException("Import session has already been used.");

            if (session.ExpiresAt <= now)
                throw new ScheduleImportSessionInvalidException("Import session has expired.");

            var sessionRows = DeserializeSessionRows(session.RowsJson);
            var rowsByNumber = sessionRows.ToDictionary(r => r.RowNumber);
            var rowsToApply = new List<ScheduleImportSessionRow>();

            foreach (var rowNumber in selectedRowNumbers)
            {
                if (!rowsByNumber.TryGetValue(rowNumber, out var row) || !IsApplicable(row))
                {
                    throw new ScheduleImportSessionInvalidException($"Selected row {rowNumber} cannot be applied.");
                }

                rowsToApply.Add(row);
            }

            await ValidateRowsBeforeApplyAsync(rowsToApply, facultyId, termId, ct);

            var sectionIds = rowsToApply
                .Select(r => r.SectionId!.Value)
                .Distinct()
                .ToList();

            var existingSchedules = await _db.SectionSchedules
                .Where(ss => sectionIds.Contains(ss.CourseSectionId))
                .ToListAsync(ct);

            _db.SectionSchedules.RemoveRange(existingSchedules);

            foreach (var row in rowsToApply)
            {
                _db.SectionSchedules.Add(new SectionSchedule
                {
                    CourseSectionId = row.SectionId!.Value,
                    TimeSlotId = row.TimeSlotId!.Value,
                    ClassroomId = row.ClassroomId!.Value,
                });
            }

            session.ConsumedAt = now;
            await _db.SaveChangesAsync(ct);

            if (transaction is not null)
                await transaction.CommitAsync(ct);

            return new ImportResultDto
            {
                AppliedCount = rowsToApply.Count,
            };
        }
        finally
        {
            if (transaction is not null)
                await transaction.DisposeAsync();
        }
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static List<ScheduleImportSessionRow> DeserializeSessionRows(string rowsJson)
    {
        try
        {
            return JsonSerializer.Deserialize<List<ScheduleImportSessionRow>>(rowsJson, SessionJsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new ScheduleImportSessionInvalidException($"Import session data is invalid: {ex.Message}");
        }
    }

    private static bool IsApplicable(ScheduleImportSessionRow row) =>
        (row.Status == "Valid" || row.Status == "Warning")
        && row.SectionId.HasValue
        && row.ClassroomId.HasValue
        && row.TimeSlotId.HasValue;

    private async Task ValidateRowsBeforeApplyAsync(
        List<ScheduleImportSessionRow> rows,
        int facultyId,
        int termId,
        CancellationToken ct)
    {
        var sectionIds = rows.Select(r => r.SectionId!.Value).Distinct().ToList();
        var timeSlotIds = rows.Select(r => r.TimeSlotId!.Value).Distinct().ToList();
        var classroomIds = rows.Select(r => r.ClassroomId!.Value).Distinct().ToList();

        var validSectionIds = await _db.CourseSections
            .Where(s => sectionIds.Contains(s.Id)
                     && s.TermId == termId
                     && s.Course.Department!.FacultyId == facultyId)
            .Select(s => s.Id)
            .ToListAsync(ct);

        if (validSectionIds.Count != sectionIds.Count)
            throw new ScheduleImportSessionInvalidException("One or more selected sections no longer match the requested faculty or term.");

        var validTimeSlotIds = await _db.TimeSlots
            .Where(ts => timeSlotIds.Contains(ts.Id) && ts.TermId == termId)
            .Select(ts => ts.Id)
            .ToListAsync(ct);

        if (validTimeSlotIds.Count != timeSlotIds.Count)
            throw new ScheduleImportSessionInvalidException("One or more selected time slots no longer match the requested term.");

        var validClassroomIds = await _db.Classrooms
            .Where(c => classroomIds.Contains(c.Id))
            .Select(c => c.Id)
            .ToListAsync(ct);

        if (validClassroomIds.Count != classroomIds.Count)
            throw new ScheduleImportSessionInvalidException("One or more selected classrooms no longer exist.");
    }

    private static ImportRowDto ToPublicRow(ScheduleImportSessionRow row) => new()
    {
        RowNumber = row.RowNumber,
        CourseCode = row.CourseCode,
        SectionCode = row.SectionCode,
        InstructorName = row.InstructorName,
        Day = row.Day,
        StartTime = row.StartTime,
        EndTime = row.EndTime,
        ClassroomName = row.ClassroomName,
        Capacity = row.Capacity,
        Status = row.Status,
        Message = row.Message,
    };

    private sealed class ScheduleImportSessionRow
    {
        public int RowNumber { get; set; }
        public string CourseCode { get; set; } = string.Empty;
        public string SectionCode { get; set; } = string.Empty;
        public string InstructorName { get; set; } = string.Empty;
        public string Day { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string ClassroomName { get; set; } = string.Empty;
        public int Capacity { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public int? SectionId { get; set; }
        public int? ClassroomId { get; set; }
        public int? TimeSlotId { get; set; }
    }

    private static string DayOfWeekTurkish(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday    => "Pazartesi",
        DayOfWeek.Tuesday   => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday  => "Perşembe",
        DayOfWeek.Friday    => "Cuma",
        DayOfWeek.Saturday  => "Cumartesi",
        DayOfWeek.Sunday    => "Pazar",
        _                   => day.ToString()
    };
}
