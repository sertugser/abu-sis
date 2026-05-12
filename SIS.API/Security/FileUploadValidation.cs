using System.IO;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Http;

namespace SIS.API.Security;

public static class FileUploadValidation
{
    public const long MaxExcelImportBytes = 5 * 1024 * 1024;

    public static string? ValidateXlsx(IFormFile? file)
    {
        if (file is null || file.Length == 0)
            return "Dosya secilmedi.";

        if (file.Length > MaxExcelImportBytes)
            return "Dosya boyutu 5MB'i gecemez.";

        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return "Sadece .xlsx dosyalari kabul edilir.";

        try
        {
            using var stream = file.OpenReadStream();
            var header = new byte[4];
            var read = stream.Read(header, 0, header.Length);
            if (read < 4 || header[0] != 0x50 || header[1] != 0x4B)
                return "Gecersiz Excel dosyasi.";
        }
        catch
        {
            return "Dosya okunamadi.";
        }

        return null;
    }

    public static string? ValidateWorkbookShape(IXLWorkbook workbook, int maxRows = 5000, int maxColumns = 100)
    {
        foreach (var worksheet in workbook.Worksheets)
        {
            var range = worksheet.RangeUsed();
            if (range is null)
                continue;

            if (range.RowCount() > maxRows || range.ColumnCount() > maxColumns)
                return $"Excel dosyası en fazla {maxRows} satır ve {maxColumns} sütun içerebilir.";
        }

        return null;
    }
}
