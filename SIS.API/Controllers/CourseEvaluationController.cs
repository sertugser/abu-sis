using System.Security.Claims;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SIS.API.Authorization;
using SIS.Application.CourseEvaluation.DTOs;
using SIS.Application.CourseEvaluation.Services;
using SIS.Domain.Enums;

namespace SIS.API.Controllers;

[ApiController]
[Route("api/course-evaluation")]
[Authorize]
public class CourseEvaluationController : ControllerBase
{
    private readonly ICourseEvaluationService _service;

    public CourseEvaluationController(ICourseEvaluationService service)
    {
        _service = service;
    }

    [HttpGet("period/active")]
    [RequirePermission("lookup.evaluation-period")]
    public async Task<IActionResult> GetActivePeriod(CancellationToken ct)
    {
        var period = await _service.GetActivePeriodAsync(ct);
        if (period == null)
            return Ok(new { isOpen = false });
        return Ok(new { isOpen = period.IsCurrentlyOpen, period });
    }

    [HttpGet("my-courses")]
    [RequirePermission("menu.ders-degerlendirme.view")]
    public async Task<IActionResult> GetMyCourses(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var courses = await _service.GetMyCoursesAsync(userId.Value, ct);
        return Ok(courses);
    }

    [HttpGet("questions")]
    [RequirePermission("menu.ders-degerlendirme.view")]
    public async Task<IActionResult> GetActiveQuestions(CancellationToken ct)
    {
        var questions = await _service.GetActiveQuestionsAsync(ct);
        return Ok(questions);
    }

    [HttpPost("submit")]
    [RequirePermission("menu.ders-degerlendirme.submit")]
    public async Task<IActionResult> Submit([FromBody] SubmitEvaluationDto dto, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        var (success, error) = await _service.SubmitEvaluationAsync(userId.Value, dto, ct);
        if (!success)
            return BadRequest(new { error });

        return Ok(new { success = true });
    }

    [HttpGet("results/section/{sectionId:int}")]
    [RequirePermission("menu.degerlendirme-sonuclari.view")]
    public async Task<IActionResult> GetSectionResults(int sectionId, CancellationToken ct)
    {
        var result = await _service.GetSectionResultsAsync(sectionId, ct);
        if (result == null)
            return NotFound(new { error = "Ders bulunamadi." });

        return Ok(result);
    }

    [HttpGet("results/department/{departmentId:int}")]
    [RequirePermission("menu.degerlendirme-sonuclari.view")]
    public async Task<IActionResult> GetDepartmentSummary(
        int departmentId,
        [FromQuery] int? termId,
        CancellationToken ct)
    {
        var summary = await _service.GetDepartmentSummaryAsync(departmentId, termId, ct);
        if (summary == null)
            return NotFound(new { error = "Bolum veya donem bulunamadi." });

        return Ok(summary);
    }

    [HttpPost("period/open")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.open")]
    public async Task<IActionResult> OpenPeriod([FromBody] OpenPeriodRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId == null)
            return Unauthorized();

        try
        {
            var period = await _service.OpenPeriodAsync(request.TermId, request.ClosesAt, userId.Value.ToString(), ct);
            return Ok(period);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("period/{periodId:int}/close")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.close")]
    public async Task<IActionResult> ClosePeriod(int periodId, CancellationToken ct)
    {
        try
        {
            var period = await _service.ClosePeriodAsync(periodId, ct);
            return Ok(period);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpGet("questions/all")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.view")]
    public async Task<IActionResult> GetAllQuestions(CancellationToken ct)
    {
        var questions = await _service.GetAllQuestionsAsync(ct);
        return Ok(questions);
    }

    [HttpPost("questions")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.create")]
    public async Task<IActionResult> CreateQuestion([FromBody] CreateEvaluationQuestionDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest(new { error = "Soru metni bos olamaz." });

        var question = await _service.CreateQuestionAsync(dto, ct);
        return Ok(question);
    }

    [HttpPut("questions/{id:int}")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.edit")]
    public async Task<IActionResult> UpdateQuestion(int id, [FromBody] UpdateEvaluationQuestionDto dto, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(dto.Text))
            return BadRequest(new { error = "Soru metni bos olamaz." });

        var question = await _service.UpdateQuestionAsync(id, dto, ct);
        if (question == null)
            return NotFound(new { error = "Soru bulunamadi." });

        return Ok(question);
    }

    [HttpDelete("questions/{id:int}")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.delete")]
    public async Task<IActionResult> DeleteQuestion(int id, CancellationToken ct)
    {
        var success = await _service.DeleteQuestionAsync(id, ct);
        if (!success)
            return NotFound(new { error = "Soru bulunamadi." });

        return NoContent();
    }

    [HttpGet("results/department/{departmentId:int}/export")]
    [RequirePermission("menu.degerlendirme-sonuclari.view")]
    public async Task<IActionResult> ExportDepartmentResults(
        int departmentId,
        [FromQuery] int? termId,
        CancellationToken ct)
    {
        var data = await _service.GetDepartmentExportDataAsync(departmentId, termId, ct);
        if (data == null)
            return NotFound(new { error = "Bolum veya donem bulunamadi." });

        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Değerlendirme Sonuçları");

        // ── Color palette ─────────────────────────────────────────────
        var cNavy       = XLColor.FromArgb(17,  46,  81);   // #112E51 dark navy
        var cBlue       = XLColor.FromArgb(0,   94, 162);   // #005EA2 medium blue
        var cIndigo     = XLColor.FromArgb(67,  56, 202);   // #4338CA indigo
        var cLightBlue  = XLColor.FromArgb(219, 234, 254);  // #DBEAfe light blue
        var cAltRow     = XLColor.FromArgb(248, 250, 253);  // near-white alt row
        var cBorder     = XLColor.FromArgb(203, 213, 225);  // #CBD5E1 border
        var cGreenText  = XLColor.FromArgb(21,  128,  61);  // dark green
        var cGreenBg    = XLColor.FromArgb(220, 252, 231);  // light green
        var cAmberText  = XLColor.FromArgb(146,  64,  14);  // dark amber
        var cAmberBg    = XLColor.FromArgb(254, 243, 199);  // light amber
        var cRedText    = XLColor.FromArgb(153,  27,  27);  // dark red
        var cRedBg      = XLColor.FromArgb(254, 226, 226);  // light red
        var cGrayText   = XLColor.FromArgb(100, 116, 139);  // slate-500

        var sections = data.Sections
            .OrderBy(s => s.CourseCode)
            .ThenBy(s => s.InstructorName)
            .ToList();

        var qCount       = data.RatingQuestions.Count;
        var courseQCount = data.RatingQuestions.Count(q => q.Category == EvaluationQuestionCategory.Course);
        var instrQCount  = data.RatingQuestions.Count(q => q.Category == EvaluationQuestionCategory.Instructor);

        // Fixed columns: #(1) Code(2) Name(3) Instructor(4) Enrolled(5) Responses(6) Rate%(7) AvgRating(8)
        const int fixedCols = 8;
        int totalCols = fixedCols + qCount;

        // ── Row 1: Main title (merged) ────────────────────────────────
        ws.Range(1, 1, 1, totalCols).Merge();
        var titleCell = ws.Cell(1, 1);
        titleCell.Value = "ANKARA BİLİM ÜNİVERSİTESİ — DERS DEĞERLENDİRME SONUÇLARI";
        titleCell.Style.Font.Bold = true;
        titleCell.Style.Font.FontSize = 13;
        titleCell.Style.Font.FontColor = XLColor.White;
        titleCell.Style.Fill.BackgroundColor = cNavy;
        titleCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        titleCell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
        ws.Row(1).Height = 30;

        // ── Row 2: Dept · Term · Date (merged) ───────────────────────
        ws.Range(2, 1, 2, totalCols).Merge();
        var infoCell = ws.Cell(2, 1);
        infoCell.Value = $"{data.DepartmentName}     ·     {data.TermName}     ·     Oluşturulma: {DateTime.Now.ToString("dd MMMM yyyy, HH:mm", new System.Globalization.CultureInfo("tr-TR"))}";
        infoCell.Style.Font.FontSize = 10;
        infoCell.Style.Font.Italic = true;
        infoCell.Style.Font.FontColor = cGrayText;
        infoCell.Style.Fill.BackgroundColor = XLColor.FromArgb(241, 245, 249);
        infoCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        infoCell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
        ws.Row(2).Height = 22;

        // ── Rows 3-4: Stats boxes ─────────────────────────────────────
        // Each stat occupies 2 rows + spans ~totalCols/4 columns
        ws.Row(3).Height = 16;
        ws.Row(4).Height = 26;

        (string Label, string Value, XLColor Accent)[] stats = {
            ("TOPLAM DERS",       data.TotalSections.ToString(),                                              cBlue),
            ("DEĞERLENDİRİLEN",   data.EvaluatedSections.ToString(),                                         cIndigo),
            ("GENEL KATILIM",     $"%{data.OverallResponseRate:F1}",                                          XLColor.FromArgb(180, 83, 9)),
            ("GENEL ORTALAMA",    data.AverageRating.HasValue ? $"{data.AverageRating.Value:F2} / 5" : "—",  XLColor.FromArgb(5, 150, 105)),
        };

        int baseW = totalCols / 4;
        int[] statEnds = { baseW, baseW * 2, baseW * 3, totalCols };
        int[] statStarts = { 1, baseW + 1, baseW * 2 + 1, baseW * 3 + 1 };

        for (int si = 0; si < 4; si++)
        {
            int sc = statStarts[si];
            int ec = statEnds[si];
            var (lbl, val, accent) = stats[si];

            ws.Range(3, sc, 3, ec).Merge();
            var lblCell = ws.Cell(3, sc);
            lblCell.Value = lbl;
            lblCell.Style.Font.FontSize = 8;
            lblCell.Style.Font.Bold = true;
            lblCell.Style.Font.FontColor = accent;
            lblCell.Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252);
            lblCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            lblCell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Bottom;
            lblCell.Style.Border.TopBorder    = XLBorderStyleValues.Medium;
            lblCell.Style.Border.LeftBorder   = si == 0 ? XLBorderStyleValues.Medium : XLBorderStyleValues.None;
            lblCell.Style.Border.RightBorder  = si == 3 ? XLBorderStyleValues.Medium : XLBorderStyleValues.Thin;
            lblCell.Style.Border.TopBorderColor   = accent;
            lblCell.Style.Border.LeftBorderColor  = cBorder;
            lblCell.Style.Border.RightBorderColor = cBorder;

            ws.Range(4, sc, 4, ec).Merge();
            var valCell = ws.Cell(4, sc);
            valCell.Value = val;
            valCell.Style.Font.FontSize = 14;
            valCell.Style.Font.Bold = true;
            valCell.Style.Font.FontColor = accent;
            valCell.Style.Fill.BackgroundColor = XLColor.FromArgb(248, 250, 252);
            valCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            valCell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Top;
            valCell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            valCell.Style.Border.LeftBorder   = si == 0 ? XLBorderStyleValues.Medium : XLBorderStyleValues.None;
            valCell.Style.Border.RightBorder  = si == 3 ? XLBorderStyleValues.Medium : XLBorderStyleValues.Thin;
            valCell.Style.Border.BottomBorderColor = cBorder;
            valCell.Style.Border.LeftBorderColor   = cBorder;
            valCell.Style.Border.RightBorderColor  = cBorder;
        }

        // ── Row 5: Spacer ─────────────────────────────────────────────
        ws.Row(5).Height = 8;

        // ── Row 6: Group headers ──────────────────────────────────────
        ws.Row(6).Height = 20;

        void SetGroupHeader(int startC, int endC, string text, XLColor bg)
        {
            ws.Range(6, startC, 6, endC).Merge();
            var c = ws.Cell(6, startC);
            c.Value = text;
            c.Style.Font.Bold = true;
            c.Style.Font.FontSize = 9;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = bg;
            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            c.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            c.Style.Border.OutsideBorderColor = XLColor.FromArgb(255, 255, 255);
        }

        SetGroupHeader(1, fixedCols, "DERS VE KATILIM BİLGİLERİ", cNavy);
        if (courseQCount > 0)
            SetGroupHeader(fixedCols + 1, fixedCols + courseQCount, "DERS DEĞERLENDİRME SORULARI", cBlue);
        if (instrQCount > 0)
            SetGroupHeader(fixedCols + courseQCount + 1, totalCols, "ÖĞRETİM ÜYESİ DEĞERLENDİRME SORULARI", cIndigo);

        // ── Row 7: Column headers ──────────────────────────────────────
        ws.Row(7).Height = 36;

        void SetColHeader(int col, string text, XLColor bg)
        {
            var c = ws.Cell(7, col);
            c.Value = text;
            c.Style.Font.Bold = true;
            c.Style.Font.FontSize = 9;
            c.Style.Font.FontColor = XLColor.White;
            c.Style.Fill.BackgroundColor = bg;
            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            c.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            c.Style.Alignment.WrapText   = true;
            c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            c.Style.Border.OutsideBorderColor = XLColor.FromArgb(255, 255, 255);
        }

        SetColHeader(1, "#",               cNavy);
        SetColHeader(2, "Ders Kodu",       cNavy);
        SetColHeader(3, "Ders Adı",        cNavy);
        SetColHeader(4, "Öğretim Üyesi",   cNavy);
        SetColHeader(5, "Kayıtlı",         cNavy);
        SetColHeader(6, "Yanıt",           cNavy);
        SetColHeader(7, "Katılım\n%",      cNavy);
        SetColHeader(8, "Genel\nOrt.",     cNavy);

        var qBgCourse = XLColor.FromArgb(30, 80, 140);
        var qBgInstr  = XLColor.FromArgb(55, 48, 163);
        for (int qi = 0; qi < qCount; qi++)
        {
            var q   = data.RatingQuestions[qi];
            var bg  = q.Category == EvaluationQuestionCategory.Course ? qBgCourse : qBgInstr;
            var hCell = ws.Cell(7, fixedCols + 1 + qi);
            hCell.Value = $"S{qi + 1}";
            hCell.CreateComment().AddText(q.Text);
            hCell.Style.Font.Bold = true;
            hCell.Style.Font.FontSize = 9;
            hCell.Style.Font.FontColor = XLColor.White;
            hCell.Style.Fill.BackgroundColor = bg;
            hCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            hCell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            hCell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            hCell.Style.Border.OutsideBorderColor = XLColor.FromArgb(255, 255, 255);
        }

        // ── Data rows (row 8+) ─────────────────────────────────────────
        const int dataStartRow = 8;
        XLColor ScoreTextColor(decimal v)  => v >= 4m ? cGreenText  : v >= 3m ? cAmberText  : cRedText;
        XLColor ScoreBgColor(decimal v)    => v >= 4m ? cGreenBg    : v >= 3m ? cAmberBg    : cRedBg;

        for (int ri = 0; ri < sections.Count; ri++)
        {
            var s   = sections[ri];
            int row = dataStartRow + ri;
            var bg  = ri % 2 == 1 ? cAltRow : XLColor.White;
            ws.Row(row).Height = 18;

            void DataCell(int col, object? value, bool center = true, bool bold = false)
            {
                var c = ws.Cell(row, col);
                if (value != null) c.Value = XLCellValue.FromObject(value);
                c.Style.Fill.BackgroundColor = bg;
                c.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
                c.Style.Border.OutsideBorderColor = cBorder;
                c.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                if (center) c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                if (bold) c.Style.Font.Bold = true;
            }

            DataCell(1, ri + 1);
            ws.Cell(row, 1).Style.Font.FontColor = cGrayText;

            DataCell(2, s.CourseCode, bold: true);
            ws.Cell(row, 2).Style.Font.FontColor = cBlue;

            DataCell(3, s.CourseName, center: false);

            DataCell(4, s.InstructorName, center: false);

            DataCell(5, s.EnrolledCount);
            ws.Cell(row, 5).Style.Font.FontColor = cGrayText;

            DataCell(6, s.ResponseCount);

            DataCell(7, null);
            var rateC = ws.Cell(row, 7);
            rateC.Value = (double)(s.ResponseRate / 100m);
            rateC.Style.NumberFormat.Format = "0.0%";
            rateC.Style.Fill.BackgroundColor = bg;
            rateC.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            rateC.Style.Font.FontColor = s.ResponseRate >= 70 ? cGreenText
                                       : s.ResponseRate >= 40 ? cAmberText
                                       : cRedText;

            DataCell(8, null, bold: true);
            var avgC = ws.Cell(row, 8);
            if (s.IsVisible && s.AverageRating.HasValue)
            {
                avgC.Value = (double)s.AverageRating.Value;
                avgC.Style.NumberFormat.Format = "0.00";
                avgC.Style.Font.Bold = true;
                avgC.Style.Font.FontColor  = ScoreTextColor(s.AverageRating.Value);
                avgC.Style.Fill.BackgroundColor = ScoreBgColor(s.AverageRating.Value);
            }
            else
            {
                avgC.Value = "—";
                avgC.Style.Font.FontColor = cGrayText;
                avgC.Style.Font.Italic = true;
            }
            avgC.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
            avgC.Style.Border.OutsideBorderColor = cBorder;
            avgC.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            avgC.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;

            for (int qi = 0; qi < qCount; qi++)
            {
                int col = fixedCols + 1 + qi;
                DataCell(col, null);
                var qc = ws.Cell(row, col);
                if (!s.IsVisible)
                {
                    qc.Value = "—";
                    qc.Style.Font.FontColor = XLColor.FromArgb(200, 200, 200);
                }
                else
                {
                    var qAvg = qi < s.QuestionAverages.Count ? s.QuestionAverages[qi] : null;
                    if (qAvg.HasValue)
                    {
                        qc.Value = (double)qAvg.Value;
                        qc.Style.NumberFormat.Format = "0.00";
                        qc.Style.Font.FontColor = ScoreTextColor(qAvg.Value);
                    }
                    else
                    {
                        qc.Value = "—";
                        qc.Style.Font.FontColor = cGrayText;
                    }
                }
            }
        }

        // ── Summary row ───────────────────────────────────────────────
        int summaryRow = dataStartRow + sections.Count;
        ws.Row(summaryRow).Height = 22;
        ws.Range(summaryRow, 1, summaryRow, 4).Merge();
        var sumLabelCell = ws.Cell(summaryRow, 1);
        sumLabelCell.Value = "BÖLÜM ORTALAMASI";
        sumLabelCell.Style.Font.Bold = true;
        sumLabelCell.Style.Font.FontSize = 9;
        sumLabelCell.Style.Font.FontColor = XLColor.White;
        sumLabelCell.Style.Fill.BackgroundColor = cNavy;
        sumLabelCell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        sumLabelCell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;

        void SumCell(int col)
        {
            var c = ws.Cell(summaryRow, col);
            c.Style.Font.Bold = true;
            c.Style.Fill.BackgroundColor = XLColor.FromArgb(226, 232, 240);
            c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            c.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            c.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            c.Style.Border.OutsideBorderColor = cBorder;
        }

        SumCell(5);
        ws.Cell(summaryRow, 5).Value = sections.Sum(s => s.EnrolledCount);
        SumCell(6);
        ws.Cell(summaryRow, 6).Value = sections.Sum(s => s.ResponseCount);
        SumCell(7);
        var overallRateCell = ws.Cell(summaryRow, 7);
        overallRateCell.Value = (double)(data.OverallResponseRate / 100m);
        overallRateCell.Style.NumberFormat.Format = "0.0%";
        SumCell(7);
        SumCell(8);
        if (data.AverageRating.HasValue)
        {
            ws.Cell(summaryRow, 8).Value = (double)data.AverageRating.Value;
            ws.Cell(summaryRow, 8).Style.NumberFormat.Format = "0.00";
            ws.Cell(summaryRow, 8).Style.Font.FontColor = ScoreTextColor(data.AverageRating.Value);
        }
        else
        {
            ws.Cell(summaryRow, 8).Value = "—";
            ws.Cell(summaryRow, 8).Style.Font.FontColor = cGrayText;
        }

        // ── AutoFilter on column headers ──────────────────────────────
        if (sections.Count > 0)
            ws.Range(7, 1, summaryRow - 1, totalCols).SetAutoFilter();

        // ── Freeze top 7 rows ─────────────────────────────────────────
        ws.SheetView.FreezeRows(7);

        // ── Footer note ───────────────────────────────────────────────
        int noteRow = summaryRow + 2;
        ws.Range(noteRow, 1, noteRow, totalCols).Merge();
        var noteCell = ws.Cell(noteRow, 1);
        noteCell.Value = "* Anonimlik koruması gereği en az 5 yanıt almayan dersler için bireysel sonuçlar gösterilmemektedir (\"—\" ile işaretlenir).";
        noteCell.Style.Font.Italic = true;
        noteCell.Style.Font.FontSize = 8;
        noteCell.Style.Font.FontColor = cGrayText;

        // ── Column widths ──────────────────────────────────────────────
        ws.Column(1).Width = 5;
        ws.Column(2).Width = 12;
        ws.Column(3).Width = 34;
        ws.Column(4).Width = 26;
        ws.Column(5).Width = 10;
        ws.Column(6).Width = 10;
        ws.Column(7).Width = 11;
        ws.Column(8).Width = 12;
        for (int qi = 0; qi < qCount; qi++)
            ws.Column(fixedCols + 1 + qi).Width = 9;

        // ── Legend sheet ──────────────────────────────────────────────
        if (qCount > 0)
        {
            var legend = workbook.Worksheets.Add("Soru Listesi");
            legend.Row(1).Height = 28;

            string[] lHeaders = { "No", "Kategori", "Soru Metni" };
            for (int c = 0; c < lHeaders.Length; c++)
            {
                var lh = legend.Cell(1, c + 1);
                lh.Value = lHeaders[c];
                lh.Style.Font.Bold = true;
                lh.Style.Font.FontColor = XLColor.White;
                lh.Style.Fill.BackgroundColor = cNavy;
                lh.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                lh.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                lh.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                lh.Style.Border.OutsideBorderColor = XLColor.White;
            }

            for (int qi = 0; qi < qCount; qi++)
            {
                var q   = data.RatingQuestions[qi];
                var bg  = qi % 2 == 1 ? cAltRow : XLColor.White;
                bool isCourse = q.Category == EvaluationQuestionCategory.Course;

                legend.Row(qi + 2).Height = 20;

                var numC = legend.Cell(qi + 2, 1);
                numC.Value = $"S{qi + 1}";
                numC.Style.Font.Bold = true;
                numC.Style.Font.FontColor = isCourse ? cBlue : cIndigo;
                numC.Style.Fill.BackgroundColor = bg;
                numC.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                numC.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                numC.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
                numC.Style.Border.OutsideBorderColor = cBorder;

                var catC = legend.Cell(qi + 2, 2);
                catC.Value = isCourse ? "Ders" : "Öğretim Üyesi";
                catC.Style.Fill.BackgroundColor = isCourse ? cLightBlue : XLColor.FromArgb(224, 220, 255);
                catC.Style.Font.FontColor = isCourse ? cBlue : cIndigo;
                catC.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                catC.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                catC.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
                catC.Style.Border.OutsideBorderColor = cBorder;

                var txtC = legend.Cell(qi + 2, 3);
                txtC.Value = q.Text;
                txtC.Style.Fill.BackgroundColor = bg;
                txtC.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                txtC.Style.Border.OutsideBorder = XLBorderStyleValues.Hair;
                txtC.Style.Border.OutsideBorderColor = cBorder;
            }

            legend.Column(1).Width = 8;
            legend.Column(2).Width = 18;
            legend.Column(3).Width = 60;
        }

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);

        var safeDeptName = data.DepartmentName.Replace(' ', '_');
        var safeTermName = data.TermName.Replace(' ', '_');
        var fileName = $"degerlendirme_{safeDeptName}_{safeTermName}.xlsx";

        return File(stream.ToArray(),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    [HttpPost("questions/{id:int}/restore")]
    [RequirePermission("menu.degerlendirme-donemi-yonetimi.delete")]
    public async Task<IActionResult> RestoreQuestion(int id, CancellationToken ct)
    {
        var success = await _service.RestoreQuestionAsync(id, ct);
        if (!success)
            return NotFound(new { error = "Soru bulunamadi veya zaten aktif." });

        return Ok(new { success = true });
    }

    private int? GetUserId()
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (claim == null || !int.TryParse(claim, out var userId))
            return null;
        return userId;
    }

    public record OpenPeriodRequest(int TermId, DateTime ClosesAt);
}
