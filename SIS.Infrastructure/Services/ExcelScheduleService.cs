using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SIS.Application.Scheduling.DTOs;
using SIS.Application.Scheduling.Services;
using SIS.Infrastructure.Persistence;

namespace SIS.Infrastructure.Services;

public class ExcelScheduleService : IExcelScheduleService
{
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
                ClassroomName = ss.Classroom.Name,
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

    public Task<ImportPreviewDto> PreviewImportAsync(Stream fileStream, int termId, int facultyId, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public Task<ImportResultDto> ApplyImportAsync(int termId, List<int> rowNumbers, ImportPreviewDto preview, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

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
