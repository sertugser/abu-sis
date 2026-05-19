using FluentAssertions;
using Microsoft.AspNetCore.Http;
using SIS.API.Security;

namespace SIS.Tests.API;

public class FileUploadValidationTests
{
    [Fact]
    public void ValidateXlsx_Allows_ZipBackedXlsxFile()
    {
        var file = BuildFormFile("grades.xlsx", [0x50, 0x4B, 0x03, 0x04]);

        var result = FileUploadValidation.ValidateXlsx(file);

        result.Should().BeNull();
    }

    [Fact]
    public void ValidateXlsx_Rejects_NonXlsxExtension()
    {
        var file = BuildFormFile("grades.csv", [0x50, 0x4B, 0x03, 0x04]);

        var result = FileUploadValidation.ValidateXlsx(file);

        result.Should().Contain(".xlsx");
    }

    [Fact]
    public void ValidateXlsx_Rejects_InvalidFileSignature()
    {
        var file = BuildFormFile("grades.xlsx", [0x25, 0x50, 0x44, 0x46]);

        var result = FileUploadValidation.ValidateXlsx(file);

        result.Should().Contain("Gecersiz");
    }

    private static IFormFile BuildFormFile(string fileName, byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        return new FormFile(stream, 0, stream.Length, "file", fileName);
    }
}
