using FluentAssertions;
using SureBudgetRequest.Infrastructure.Storage;
using Xunit;

namespace SureBudgetRequest.Infrastructure.Tests.Storage;

public class StorageOptionsTests
{
    [Fact]
    public void StorageOptions_ShouldHaveCorrectDefaults()
    {
        var options = new StorageOptions();

        StorageOptions.SectionName.Should().Be("Storage");
        options.Provider.Should().Be("Supabase");
        options.AttachmentsRoot.Should().Be("App_Data/attachments");
        options.TempCleanIntervalHours.Should().Be(6);
        options.TempExpirationHours.Should().Be(24);
    }

    [Fact]
    public void StorageOptions_ShouldAllowPropertyMutation()
    {
        var options = new StorageOptions
        {
            Provider = "Local",
            AttachmentsRoot = "/var/data/attachments",
            TempCleanIntervalHours = 12,
            TempExpirationHours = 48
        };

        options.Provider.Should().Be("Local");
        options.AttachmentsRoot.Should().Be("/var/data/attachments");
        options.TempCleanIntervalHours.Should().Be(12);
        options.TempExpirationHours.Should().Be(48);
    }

    [Fact]
    public void SupabaseStorageOptions_ShouldHaveCorrectDefaults()
    {
        var options = new SupabaseStorageOptions();

        SupabaseStorageOptions.SectionName.Should().Be("Supabase");
        options.Url.Should().BeEmpty();
        options.ServiceRoleKey.Should().BeEmpty();
        options.AttachmentsBucket.Should().Be("budget-attachments");
    }

    [Fact]
    public void SupabaseStorageOptions_ShouldAllowPropertyMutation()
    {
        var options = new SupabaseStorageOptions
        {
            Url = "https://example.supabase.co",
            ServiceRoleKey = "secret-service-role-key-xyz",
            AttachmentsBucket = "custom-attachments"
        };

        options.Url.Should().Be("https://example.supabase.co");
        options.ServiceRoleKey.Should().Be("secret-service-role-key-xyz");
        options.AttachmentsBucket.Should().Be("custom-attachments");
    }
}
