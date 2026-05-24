using FluentAssertions;
using SIS.Application.Common.Services;

namespace SIS.Tests.Services;

public class StudentDocumentValidatorTests
{
    private static readonly byte[] PdfHeader = { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0 };

    [Fact]
    public void Validate_ReturnsNull_ForValidPdf()
    {
        var error = StudentDocumentValidator.Validate(
            hasFile: true, sizeBytes: 100, maxBytes: 5_000_000,
            contentType: "application/pdf", header: PdfHeader);

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_ForValidJpeg()
    {
        var error = StudentDocumentValidator.Validate(
            hasFile: true, sizeBytes: 100, maxBytes: 5_000_000,
            contentType: "image/jpeg", header: JpegHeader);

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_ForValidPng()
    {
        var error = StudentDocumentValidator.Validate(
            hasFile: true, sizeBytes: 100, maxBytes: 5_000_000,
            contentType: "image/png", header: PngHeader);

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_Rejects_WhenNoFile()
    {
        var error = StudentDocumentValidator.Validate(
            hasFile: false, sizeBytes: 0, maxBytes: 5_000_000,
            contentType: null, header: ReadOnlySpan<byte>.Empty);

        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_Rejects_EmptyFile()
    {
        var error = StudentDocumentValidator.Validate(
            hasFile: true, sizeBytes: 0, maxBytes: 5_000_000,
            contentType: "application/pdf", header: PdfHeader);

        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_Rejects_OversizeFile()
    {
        var error = StudentDocumentValidator.Validate(
            hasFile: true, sizeBytes: 6_000_000, maxBytes: 5_000_000,
            contentType: "application/pdf", header: PdfHeader);

        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_Rejects_DisallowedContentType()
    {
        var error = StudentDocumentValidator.Validate(
            hasFile: true, sizeBytes: 100, maxBytes: 5_000_000,
            contentType: "image/gif", header: PngHeader);

        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_Rejects_MagicByteMismatch_DeclaredPdfButPngBytes()
    {
        var error = StudentDocumentValidator.Validate(
            hasFile: true, sizeBytes: 100, maxBytes: 5_000_000,
            contentType: "application/pdf", header: PngHeader);

        error.Should().NotBeNull();
    }

    [Fact]
    public void MagicBytesMatch_False_WhenHeaderTooShort()
    {
        StudentDocumentValidator.MagicBytesMatch("application/pdf", new byte[] { 0x25, 0x50 }).Should().BeFalse();
    }
}
