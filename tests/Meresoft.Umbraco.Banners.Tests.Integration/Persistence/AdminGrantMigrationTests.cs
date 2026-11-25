using Meresoft.Umbraco.Banners.Persistence.Migrations;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Migrations;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Migrations;
using Umbraco.Cms.Infrastructure.Migrations.Upgrade;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Persistence;

[TestFixture]
public class AdminGrantMigrationTests : BannersIntegrationTestBase
{
    private IUserGroupService UserGroupService => GetRequiredService<IUserGroupService>();

    /// <summary>A step that does nothing: stands for any later migration step of the package.</summary>
    public sealed class LaterStep : AsyncMigrationBase
    {
        public LaterStep(IMigrationContext context) : base(context)
        {
        }

        protected override Task MigrateAsync() => Task.CompletedTask;
    }

    [Test]
    public async Task FreshInstall_GrantsSectionAndHtmlPermission_ToAdministratorsOnly()
    {
        await InstallPackageAsync();

        IEnumerable<IUserGroup> groups = await UserGroupService.GetAllAsync(0, 1000).ContinueWith(t => t.Result.Items);
        IUserGroup admins = groups.Single(g => g.Key == Constants.Security.AdminGroupKey);

        Assert.Multiple(() =>
        {
            Assert.That(admins.AllowedSections, Does.Contain(BannersConstants.SectionAlias));
            Assert.That(admins.Permissions, Does.Contain(BannersConstants.HtmlPermission));
        });

        foreach (IUserGroup other in groups.Where(g => g.Key != Constants.Security.AdminGroupKey))
        {
            Assert.That(other.AllowedSections, Does.Not.Contain(BannersConstants.SectionAlias), $"section granted to {other.Alias}");
            Assert.That(other.Permissions, Does.Not.Contain(BannersConstants.HtmlPermission), $"permission granted to {other.Alias}");
        }
    }

    [Test]
    public async Task RunningThePlanAgain_DoesNotRestoreWhatAnAdministratorRemoved()
    {
        await InstallPackageAsync();
        await RemoveGrantsFromAdministratorsAsync();

        await InstallPackageAsync();

        await AssertAdministratorsHaveNoGrantsAsync();
    }

    [Test]
    public async Task ALaterMigrationStep_DoesNotRestoreWhatAnAdministratorRemoved()
    {
        await InstallPackageAsync();
        await RemoveGrantsFromAdministratorsAsync();

        // A plan with the same name that continues from the final state of the package plan,
        // as a future version of the package would.
        string finalState = new BannersMigrationPlan().FinalState;
        var laterPlan = new MigrationPlan(BannersConstants.MigrationPlanName);
        laterPlan.From(finalState).To<LaterStep>("5d1f6e0a-61c7-4a55-9f0e-0b8a5e3e2a10");

        var upgrader = new Upgrader(laterPlan);
        await upgrader.ExecuteAsync(
            GetRequiredService<IMigrationPlanExecutor>(),
            GetRequiredService<ICoreScopeProvider>(),
            GetRequiredService<IKeyValueService>());

        await AssertAdministratorsHaveNoGrantsAsync();
    }

    /// <summary>The plan state of an install that ran only the first two steps, before the HTML verb step existed.</summary>
    private const string StateAfterFirstGrant = "c9a4f1d2-3b8e-4e6a-b7d1-2e5f9c3a7b02";

    private void RewindThePlanToTheFirstGrant() =>
        GetRequiredService<IKeyValueService>().SetValue("Umbraco.Core.Upgrader.State+" + BannersConstants.MigrationPlanName, StateAfterFirstGrant);

    private async Task RemoveOnlyTheHtmlVerbAsync()
    {
        IUserGroup admins = (await UserGroupService.GetAsync(Constants.Security.AdminGroupKey))!;
        admins.Permissions.Remove(BannersConstants.HtmlPermission);
        await UserGroupService.UpdateAsync(admins, Constants.Security.SuperUserKey);
    }

    [Test]
    public async Task AnInstallThatLacksTheHtmlVerb_ButHasTheSection_GetsTheVerbFromTheNewStep()
    {
        await InstallPackageAsync();
        await RemoveOnlyTheHtmlVerbAsync();
        RewindThePlanToTheFirstGrant();

        await InstallPackageAsync();

        IUserGroup admins = (await UserGroupService.GetAsync(Constants.Security.AdminGroupKey))!;
        Assert.Multiple(() =>
        {
            Assert.That(admins.Permissions, Does.Contain(BannersConstants.HtmlPermission));
            Assert.That(admins.AllowedSections, Does.Contain(BannersConstants.SectionAlias));
        });
    }

    [Test]
    public async Task TheNewStep_IsIdempotent_AndLeavesOtherGroupsAlone()
    {
        await InstallPackageAsync();
        IUserGroup before = (await UserGroupService.GetAsync(Constants.Security.AdminGroupKey))!;
        string[] sections = before.AllowedSections.OrderBy(x => x).ToArray();
        string[] permissions = before.Permissions.OrderBy(x => x).ToArray();

        RewindThePlanToTheFirstGrant();
        await InstallPackageAsync(); // runs the new step on an install that already has the verb
        RewindThePlanToTheFirstGrant();
        await InstallPackageAsync(); // and again

        IUserGroup after = (await UserGroupService.GetAsync(Constants.Security.AdminGroupKey))!;
        Assert.Multiple(() =>
        {
            Assert.That(after.AllowedSections.OrderBy(x => x), Is.EqualTo(sections));
            Assert.That(after.Permissions.OrderBy(x => x), Is.EqualTo(permissions));
        });
        foreach (IUserGroup other in (await UserGroupService.GetAllAsync(0, 1000)).Items.Where(g => g.Key != Constants.Security.AdminGroupKey))
        {
            Assert.That(other.Permissions, Does.Not.Contain(BannersConstants.HtmlPermission), other.Alias);
        }
    }

    [Test]
    public async Task TheNewStep_DoesNotGiveTheVerbBack_WhenAdministratorsNoLongerHaveTheSection()
    {
        await InstallPackageAsync();
        await RemoveGrantsFromAdministratorsAsync();
        RewindThePlanToTheFirstGrant();

        await InstallPackageAsync();

        await AssertAdministratorsHaveNoGrantsAsync();
    }

    private async Task RemoveGrantsFromAdministratorsAsync()
    {
        IUserGroup admins = (await UserGroupService.GetAsync(Constants.Security.AdminGroupKey))!;
        admins.RemoveAllowedSection(BannersConstants.SectionAlias);
        admins.Permissions.Remove(BannersConstants.HtmlPermission);
        await UserGroupService.UpdateAsync(admins, Constants.Security.SuperUserKey);
    }

    private async Task AssertAdministratorsHaveNoGrantsAsync()
    {
        IUserGroup admins = (await UserGroupService.GetAsync(Constants.Security.AdminGroupKey))!;
        Assert.Multiple(() =>
        {
            Assert.That(admins.AllowedSections, Does.Not.Contain(BannersConstants.SectionAlias));
            Assert.That(admins.Permissions, Does.Not.Contain(BannersConstants.HtmlPermission));
        });
    }
}
