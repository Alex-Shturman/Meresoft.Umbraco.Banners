using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Core.Configuration.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

/// <summary>
/// Pins what the README says about history length: Umbraco's own log cleanup (the log scrubber job calls
/// <c>IAuditService.CleanLogsAsync</c> with <c>Umbraco:CMS:Logging:MaxLogAge</c>) removes only the rows whose header
/// is "open" or "system", never the rows the package writes (New, Save, Publish, Unpublish, Copy, Delete).
/// </summary>
[TestFixture]
public class AuditRetentionTests : BannersServiceTestBase
{
    private const int NodeId = 4242;

    private void InsertRow(string header, string entityType, int daysOld)
    {
        using IScope scope = ScopeProvider.CreateScope();
        scope.Database.Execute(
            "INSERT INTO umbracoLog (userId, NodeId, entityType, Datestamp, logHeader, logComment) VALUES (@0, @1, @2, @3, @4, @5)",
            -1, NodeId, entityType, DateTime.UtcNow.AddDays(-daysOld), header, "invented row");
        scope.Complete();
    }

    private string[] RemainingHeaders()
    {
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);
        return scope.Database.Fetch<string>("SELECT logHeader FROM umbracoLog WHERE NodeId = @0 ORDER BY id", NodeId).ToArray();
    }

    [Test]
    public async Task TheUmbracoLogCleanup_KeepsEveryRowThePackageWrites_EvenWhenOld()
    {
        foreach (string header in new[] { "New", "Save", "Publish", "Unpublish", "Copy", "Delete" })
        {
            InsertRow(header, BannersConstants.BannerEntityType, daysOld: 800);
        }

        InsertRow("open", "Document", daysOld: 800);
        InsertRow("system", "Document", daysOld: 800);

        await GetRequiredService<IAuditService>().CleanLogsAsync((int)TimeSpan.FromDays(1).TotalMinutes);

        Assert.That(RemainingHeaders(), Is.EqualTo(new[] { "New", "Save", "Publish", "Unpublish", "Copy", "Delete" }), "only the open and system rows are cleaned");
    }

    [Test]
    public void TheDefaultMaxLogAge_IsOneDay_AsTheReadmeSays() =>
        Assert.That(new LoggingSettings().MaxLogAge, Is.EqualTo(TimeSpan.FromDays(1)));
}
