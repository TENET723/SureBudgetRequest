using FluentAssertions;
using SureBudgetRequest.Application.BudgetRequests.Common;
using Xunit;

namespace SureBudgetRequest.Application.Tests.BudgetRequests.Common;

public class AttachmentConstraintsTests
{
    // =========================================================================
    // 1. Constants Invariants
    // =========================================================================

    [Fact]
    public void MaxBytes_ShouldBeExactlyFiftyMegabytes()
    {
        const long expectedBytes = 50L * 1024 * 1024;
        AttachmentConstraints.MaxBytes.Should().Be(expectedBytes);
        AttachmentConstraints.MaxBytes.Should().Be(52_428_800L);
    }

    [Fact]
    public void MaxFilesPerRequest_ShouldBeTen()
    {
        AttachmentConstraints.MaxFilesPerRequest.Should().Be(10);
    }

    // =========================================================================
    // 2. AllowedContentTypes Collection
    // =========================================================================

    [Fact]
    public void AllowedContentTypes_ShouldContainExpectedElevenMimeTypes()
    {
        var expected = new[]
        {
            "application/pdf",
            "image/png",
            "image/jpeg",
            "image/gif",
            "image/webp",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "text/plain",
            "text/csv"
        };

        AttachmentConstraints.AllowedContentTypes.Should().HaveCount(11);
        AttachmentConstraints.AllowedContentTypes.Should().Contain(expected);
    }

    // =========================================================================
    // 3. IsAllowedContentType - Accepted MIME Types
    // =========================================================================

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("APPLICATION/PDF")]
    [InlineData("Application/Pdf")]
    [InlineData("image/png")]
    [InlineData("IMAGE/PNG")]
    [InlineData("image/jpeg")]
    [InlineData("image/gif")]
    [InlineData("image/webp")]
    [InlineData("application/msword")]
    [InlineData("application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("application/vnd.ms-excel")]
    [InlineData("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet")]
    [InlineData("text/plain")]
    [InlineData("TEXT/PLAIN")]
    [InlineData("text/csv")]
    [InlineData("Text/Csv")]
    public void IsAllowedContentType_ValidMimeTypes_ShouldReturnTrue(string contentType)
    {
        // Act & Assert
        AttachmentConstraints.IsAllowedContentType(contentType).Should().BeTrue();
    }

    // =========================================================================
    // 4. IsAllowedContentType - Rejected MIME Types & Edge Cases
    // =========================================================================

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("application/x-msdownload")]
    [InlineData("application/octet-stream")]
    [InlineData("application/javascript")]
    [InlineData("text/html")]
    [InlineData("application/zip")]
    [InlineData("application/x-rar-compressed")]
    [InlineData("application/x-7z-compressed")]
    [InlineData("application/x-sh")]
    [InlineData("video/mp4")]
    [InlineData("audio/mpeg")]
    [InlineData("application/json")]
    [InlineData("application/xml")]
    [InlineData("text/xml")]
    [InlineData("application/vnd.android.package-archive")]
    public void IsAllowedContentType_DisallowedOrEmptyTypes_ShouldReturnFalse(string? contentType)
    {
        // Act & Assert
        AttachmentConstraints.IsAllowedContentType(contentType).Should().BeFalse();
    }
}
