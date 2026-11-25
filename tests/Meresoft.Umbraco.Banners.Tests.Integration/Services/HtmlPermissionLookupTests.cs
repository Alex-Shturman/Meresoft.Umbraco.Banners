using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Selection;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Audit;
using Meresoft.Umbraco.Banners.Services.Authorization;
using Meresoft.Umbraco.Banners.Services.Models;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

/// <summary>
/// A save looks the user up only when the request could touch html, so ordinary image banner saves do not load a user
/// and its groups. The lookup must still happen, and fail closed, whenever html could change.
/// </summary>
[TestFixture]
public class HtmlPermissionLookupTests : BannersServiceTestBase
{
    private sealed class CountingChecker : HtmlPermissionChecker
    {
        public CountingChecker(IUserService users, bool answer)
            : base(users)
        {
            Answer = answer;
        }

        public int Lookups { get; private set; }

        public bool Answer { get; }

        public override Task<bool> IsAllowedAsync(Guid userKey)
        {
            Lookups++;
            return Task.FromResult(Answer);
        }
    }

    private CountingChecker _checker = null!;
    private BannerService _service = null!;
    private BannerZone _zone = null!;

    private BannerService NewService(CountingChecker checker) => new(
        GetRequiredService<IScopeProvider>(),
        GetRequiredService<IBannerRepository>(),
        GetRequiredService<IZoneRepository>(),
        GetRequiredService<BannerAuditWriter>(),
        GetRequiredService<IPublishedMediaCache>(),
        GetRequiredService<BannerStateCalculator>(),
        GetRequiredService<TimeProvider>(),
        checker);

    [SetUp]
    public async Task Prepare()
    {
        _zone = await NewZoneAsync("lookup-zone");
        _checker = new CountingChecker(GetRequiredService<IUserService>(), answer: false);
        _service = NewService(_checker);
    }

    [Test]
    public async Task SavingAnImageBanner_WithNoHtml_DoesNotLookTheUserUp()
    {
        Banner banner = await NewBannerAsync(ImageDraft(NewMediaKey(), _zone.Key));
        BannerDraftModel change = ImageDraft(banner.Draft!.MediaKey!.Value, _zone.Key);
        change.Name = "Renamed promo";
        change.UpdateDate = banner.UpdateDate;

        var attempt = await _service.SaveDraftAsync(banner.Key, change, User);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Success, Is.True, attempt.Status.ToString());
            Assert.That(_checker.Lookups, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task SavingAnImageBanner_WithHtmlText_LooksTheUserUp_AndFailsClosed()
    {
        Banner banner = await NewBannerAsync(ImageDraft(NewMediaKey(), _zone.Key));
        BannerDraftModel change = ImageDraft(banner.Draft!.MediaKey!.Value, _zone.Key);
        change.Html = "<b>x</b>";
        change.UpdateDate = banner.UpdateDate;

        var attempt = await _service.SaveDraftAsync(banner.Key, change, User);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Status, Is.EqualTo(BannerOperationStatus.HtmlPermissionRequired));
            Assert.That(_checker.Lookups, Is.EqualTo(1));
        });
    }

    [Test]
    public async Task SavingAnHtmlBanner_WithItsHtmlUnchanged_DoesNotLookTheUserUp()
    {
        // The administrator (the super user group holds the verb) creates the Html banner with the real checker.
        var created = await BannerService.CreateAsync(
            new BannerCreateModel { Type = BannerType.Html, Draft = new BannerDraftModel { Name = "Html offer", Html = "<p>x</p>", Weight = 1, ZoneKeys = [_zone.Key] } },
            User);
        Assert.That(created.Success, Is.True, created.Status.ToString());
        BannerDraftModel change = new() { Name = "Renamed", Html = "<p>x</p>", Weight = 1, ZoneKeys = [_zone.Key], UpdateDate = created.Result!.UpdateDate };

        var attempt = await _service.SaveDraftAsync(created.Result.Key, change, User);

        Assert.Multiple(() =>
        {
            Assert.That(attempt.Success, Is.True, attempt.Status.ToString());
            Assert.That(_checker.Lookups, Is.EqualTo(0));
        });
    }

    [Test]
    public async Task SavingAnHtmlBanner_WithNewHtml_LooksTheUserUp()
    {
        var created = await BannerService.CreateAsync(
            new BannerCreateModel { Type = BannerType.Html, Draft = new BannerDraftModel { Name = "Html offer", Html = "<p>x</p>", Weight = 1, ZoneKeys = [_zone.Key] } },
            User);
        BannerDraftModel change = new() { Name = "Html offer", Html = "<p>changed</p>", Weight = 1, ZoneKeys = [_zone.Key], UpdateDate = created.Result!.UpdateDate };

        var refused = await _service.SaveDraftAsync(created.Result.Key, change, User);
        var allowedService = NewService(new CountingChecker(GetRequiredService<IUserService>(), answer: true));
        var allowed = await allowedService.SaveDraftAsync(created.Result.Key, change, User);

        Assert.Multiple(() =>
        {
            Assert.That(refused.Status, Is.EqualTo(BannerOperationStatus.HtmlPermissionRequired));
            Assert.That(allowed.Success, Is.True, allowed.Status.ToString());
        });
    }
}
