using FluentAssertions;
using TarlaAsistani.Application.Features.Media.Commands;

namespace TarlaAsistani.UnitTests.Features.Media;

public class UploadMediaCommandValidatorTests
{
    private static readonly Guid OwnerId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task ValidJpeg_IsAccepted()
    {
        var result = await Validate("leaf.jpg", "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF, 0xD9 });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task PngBytesWithJpegMimeAndExtension_AreRejected()
    {
        var result = await Validate("leaf.jpg", "image/jpeg", new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task PngBytesWithJpegExtension_AreRejected()
    {
        var result = await Validate("leaf.jpg", "image/png", new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task ExecutableBytesAreRejectedBeforeStorage()
    {
        var result = await Validate("leaf.jpg", "image/jpeg", new byte[] { 0x4D, 0x5A, 0x90, 0x00 });

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task AudioMimesRemainHeaderValidatedOnly()
    {
        var result = await Validate("note.m4a", "audio/mp4", new byte[] { 0x00, 0x01, 0x02 });

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task MissingOrMismatchedExtensionIsRejected()
    {
        var result = await Validate("leaf.bin", "image/png", new byte[]
        {
            0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
        });

        result.IsValid.Should().BeFalse();
    }

    private static Task<FluentValidation.Results.ValidationResult> Validate(
        string fileName,
        string contentType,
        byte[] data)
    {
        var command = new UploadMediaCommand(OwnerId, fileName, contentType, data);
        return new UploadMediaCommandValidator().ValidateAsync(command);
    }
}
