using FluentAssertions;
using SIS.Application.Common.Services;

namespace SIS.Tests.Services;

public class StudentPhotoValidatorTests
{
    private static readonly byte[] PngHeader = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };
    private static readonly byte[] JpegHeader = { 0xFF, 0xD8, 0xFF, 0xE0 };

    [Fact]
    public void Validate_ReturnsNull_ForValidPng()
    {
        var error = StudentPhotoValidator.Validate(
            hasFile: true, sizeBytes: 100, maxBytes: 2_000_000,
            contentType: "image/png", header: PngHeader);

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_ReturnsNull_ForValidJpeg()
    {
        var error = StudentPhotoValidator.Validate(
            hasFile: true, sizeBytes: 100, maxBytes: 2_000_000,
            contentType: "image/jpeg", header: JpegHeader);

        error.Should().BeNull();
    }

    [Fact]
    public void Validate_Rejects_WhenNoFile()
    {
        var error = StudentPhotoValidator.Validate(
            hasFile: false, sizeBytes: 0, maxBytes: 2_000_000,
            contentType: null, header: ReadOnlySpan<byte>.Empty);

        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_Rejects_EmptyFile()
    {
        var error = StudentPhotoValidator.Validate(
            hasFile: true, sizeBytes: 0, maxBytes: 2_000_000,
            contentType: "image/png", header: PngHeader);

        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_Rejects_OversizeFile()
    {
        var error = StudentPhotoValidator.Validate(
            hasFile: true, sizeBytes: 3_000_000, maxBytes: 2_000_000,
            contentType: "image/png", header: PngHeader);

        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_Rejects_DisallowedContentType()
    {
        var error = StudentPhotoValidator.Validate(
            hasFile: true, sizeBytes: 100, maxBytes: 2_000_000,
            contentType: "image/gif", header: PngHeader);

        error.Should().NotBeNull();
    }

    [Fact]
    public void Validate_Rejects_MagicByteMismatch()
    {
        // Declared PNG but bytes are JPEG-ish.
        var error = StudentPhotoValidator.Validate(
            hasFile: true, sizeBytes: 100, maxBytes: 2_000_000,
            contentType: "image/png", header: JpegHeader);

        error.Should().NotBeNull();
    }

    [Fact]
    public void MagicBytesMatch_False_WhenHeaderTooShort()
    {
        StudentPhotoValidator.MagicBytesMatch("image/png", new byte[] { 0x89, 0x50 }).Should().BeFalse();
    }
}
