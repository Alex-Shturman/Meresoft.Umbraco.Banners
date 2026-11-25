using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using NSubstitute;
using NUnit.Framework;
using Umbraco.Cms.Core.Media;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PropertyEditors.ValueConverters;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Routing;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Caching;

[TestFixture]
public class LiveSnapshotBuilderTests
{
    private static readonly Guid MediaA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid MediaB = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");

    private IPublishedMediaCache _mediaCache = null!;
    private IImageUrlGenerator _images = null!;
    private IMediaUrlProvider _urlProvider = null!;
    private LiveSnapshotBuilder _builder = null!;

    [SetUp]
    public void SetUp()
    {
        _mediaCache = Substitute.For<IPublishedMediaCache>();
        _images = Substitute.For<IImageUrlGenerator>();
        _images.GetImageUrl(Arg.Any<ImageUrlGenerationOptions>())
            .Returns(call =>
            {
                ImageUrlGenerationOptions o = call.Arg<ImageUrlGenerationOptions>();
                return $"{o.ImageUrl}?width={o.Width}&height={o.Height}";
            });
        _urlProvider = Substitute.For<IMediaUrlProvider>();
        _urlProvider.GetMediaUrl(default!, default!, default, default, default!).ReturnsForAnyArgs(call => UrlInfo.AsMessage("not mine", "test", null));
        _builder = new LiveSnapshotBuilder(_mediaCache, new MediaUrlProviderCollection(() => [_urlProvider]), _images);
        AddMedia(MediaA, "/media/a/summer.png");
        AddMedia(MediaB, "/media/b/winter.png");
    }

    private void AddMedia(Guid key, string url)
    {
        IPublishedContent content = Substitute.For<IPublishedContent>();
        content.Key.Returns(key);
        IPublishedProperty file = Substitute.For<IPublishedProperty>();
        file.GetValue(Arg.Any<string?>(), Arg.Any<string?>()).Returns(new ImageCropperValue { Src = url });
        content.GetProperty("umbracoFile").Returns(file);
        _mediaCache.GetByIdAsync(key).Returns(Task.FromResult<IPublishedContent?>(content));
    }

    private static BannerZone Zone(int id, string alias, int? width = null, int? height = null) => new()
    {
        Id = id,
        Key = Guid.NewGuid(),
        Alias = alias,
        Name = alias,
        ShowCount = 2,
        ImageWidth = width,
        ImageHeight = height,
        RenderMode = ZoneRenderMode.Server,
    };

    private static Banner Published(int id, Guid mediaKey, params int[] zoneIds) => new()
    {
        Id = id,
        Key = Guid.NewGuid(),
        Name = "Banner " + id,
        Type = BannerType.Image,
        IsPublished = true,
        Live = new BannerVersion
        {
            Kind = BannerVersionKind.Live,
            MediaKey = mediaKey,
            LinkUrl = "https://example.org/show/" + id,
            OpenInNewWindow = true,
            AltText = "Invented alt " + id,
            Title = "Invented title " + id,
            Weight = 5,
            StartUtc = new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            EndUtc = new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc),
            ZoneIds = [.. zoneIds],
        },
    };

    [Test]
    public async Task OnlyPublishedBannersWithALiveVersionAndAnAssignmentBecomeCandidates()
    {
        BannerZone zone = Zone(1, "home-top");
        Banner shown = Published(10, MediaA, 1);
        Banner unpublished = Published(11, MediaA, 1);
        unpublished.IsPublished = false;
        Banner neverPublished = Published(12, MediaA, 1);
        neverPublished.Live = null;
        Banner notPlaced = Published(13, MediaA);

        LiveSnapshot snapshot = await _builder.BuildAsync([zone], [shown, unpublished, neverPublished, notPlaced]);

        Assert.That(snapshot.Zones["home-top"].Candidates.Select(c => c.BannerId), Is.EqualTo(new[] { 10 }));
    }

    [Test]
    public async Task CandidateCarriesTheLiveFieldsAndTheUrlFromTheMediaItem()
    {
        LiveSnapshot snapshot = await _builder.BuildAsync([Zone(1, "home-top")], [Published(10, MediaA, 1)]);

        SnapshotBanner candidate = snapshot.Zones["home-top"].Candidates.Single();
        Assert.Multiple(() =>
        {
            Assert.That(candidate.ImageUrl, Is.EqualTo("/media/a/summer.png"));
            Assert.That(candidate.LinkUrl, Is.EqualTo("https://example.org/show/10"));
            Assert.That(candidate.OpenInNewWindow, Is.True);
            Assert.That(candidate.AltText, Is.EqualTo("Invented alt 10"));
            Assert.That(candidate.Title, Is.EqualTo("Invented title 10"));
            Assert.That(candidate.Weight, Is.EqualTo(5));
            Assert.That(candidate.StartUtc, Is.EqualTo(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc)));
            Assert.That(candidate.EndUtc, Is.EqualTo(new DateTime(2027, 2, 1, 0, 0, 0, DateTimeKind.Utc)));
        });
    }

    [Test]
    public async Task TheImageUrlUsesTheZoneWidthAndHeightAndIsBuiltOnce()
    {
        LiveSnapshot snapshot = await _builder.BuildAsync([Zone(1, "home-top", 728, 90)], [Published(10, MediaA, 1)]);

        SnapshotBanner candidate = snapshot.Zones["home-top"].Candidates.Single();
        Assert.That(candidate.ImageUrl, Is.EqualTo("/media/a/summer.png?width=728&height=90"));
        Assert.That(candidate.Width, Is.EqualTo(728));
        Assert.That(candidate.Height, Is.EqualTo(90));
        _images.Received(1).GetImageUrl(Arg.Any<ImageUrlGenerationOptions>());
    }

    [Test]
    public async Task AZoneWithoutAFixedSizeUsesTheOriginalUrl()
    {
        await _builder.BuildAsync([Zone(1, "home-top")], [Published(10, MediaA, 1)]);

        _images.DidNotReceiveWithAnyArgs().GetImageUrl(default!);
    }

    [Test]
    public async Task ABannerWhoseMediaItemIsMissingIsLeftOutAndReported()
    {
        Banner missing = Published(10, Guid.Parse("aaaaaaaa-0000-0000-0000-0000000000ff"), 1);
        Banner fine = Published(11, MediaB, 1);

        LiveSnapshot snapshot = await _builder.BuildAsync([Zone(1, "home-top")], [missing, fine]);

        Assert.That(snapshot.Zones["home-top"].Candidates.Select(c => c.BannerId), Is.EqualTo(new[] { 11 }));
        Assert.That(snapshot.ImageMissingBannerIds, Is.EquivalentTo(new[] { 10 }));
    }

    [Test]
    public async Task ZonesAreKeyedByTheNormalisedAlias()
    {
        LiveSnapshot snapshot = await _builder.BuildAsync([Zone(1, " Home-Top ")], []);

        Assert.That(snapshot.Zones.ContainsKey("home-top"), Is.True);
        Assert.That(snapshot.Zones["home-top"].Candidates, Is.Empty);
        Assert.That(snapshot.Zones["home-top"].ShowCount, Is.EqualTo(2));
    }

    [Test]
    public async Task ABannerInTwoZonesIsACandidateInBoth()
    {
        LiveSnapshot snapshot = await _builder.BuildAsync(
            [Zone(1, "home-top"), Zone(2, "sidebar", 300, 250)],
            [Published(10, MediaA, 1, 2)]);

        Assert.That(snapshot.Zones["home-top"].Candidates.Single().ImageUrl, Is.EqualTo("/media/a/summer.png"));
        Assert.That(snapshot.Zones["sidebar"].Candidates.Single().ImageUrl, Does.Contain("width=300"));
    }

    [Test]
    public async Task MediaKeysTreatedAsMissing_AreLeftOutAndReported_EvenWhenTheMediaCacheStillHasThem()
    {
        LiveSnapshot snapshot = await _builder.BuildAsync(
            [Zone(1, "home-top")],
            [Published(10, MediaA, 1), Published(11, MediaB, 1)],
            new HashSet<Guid> { MediaA });

        Assert.That(snapshot.Zones["home-top"].Candidates.Select(c => c.BannerId), Is.EqualTo(new[] { 11 }));
        Assert.That(snapshot.ImageMissingBannerIds, Is.EquivalentTo(new[] { 10 }));
        Assert.That(snapshot.MediaKeys, Does.Contain(MediaA), "the key stays known, so a later change to it is noticed");
    }

    [Test]
    public async Task AMediaUrlProviderThatAnswers_IsUsedWithoutARequest_AndItsAddressWins()
    {
        _urlProvider.GetMediaUrl(Arg.Any<IPublishedContent>(), "umbracoFile", UrlMode.Relative, Arg.Any<string?>(), Arg.Any<Uri>())
            .Returns(UrlInfo.AsUrl("https://cdn.example.org/media/a/summer.png", "cdn", null));

        LiveSnapshot snapshot = await _builder.BuildAsync([Zone(1, "home-top")], [Published(10, MediaA, 1)]);

        Assert.That(snapshot.Zones["home-top"].Candidates.Single().ImageUrl, Is.EqualTo("https://cdn.example.org/media/a/summer.png"));
    }

    [Test]
    public async Task AProviderRelativeAddress_IsKeptRelative()
    {
        _urlProvider.GetMediaUrl(Arg.Any<IPublishedContent>(), "umbracoFile", UrlMode.Relative, Arg.Any<string?>(), Arg.Any<Uri>())
            .Returns(UrlInfo.FromUri(new Uri("/base/media/a/summer.png", UriKind.Relative), "default", null));

        LiveSnapshot snapshot = await _builder.BuildAsync([Zone(1, "home-top")], [Published(10, MediaA, 1)]);

        Assert.That(snapshot.Zones["home-top"].Candidates.Single().ImageUrl, Is.EqualTo("/base/media/a/summer.png"));
    }

    [Test]
    public async Task AnAbsoluteAddressOnThePlaceholderHost_IsStoredAsPathAndQuery()
    {
        // A provider that ignores the relative mode and builds the address from the base it was given.
        _urlProvider.GetMediaUrl(Arg.Any<IPublishedContent>(), "umbracoFile", UrlMode.Relative, Arg.Any<string?>(), Arg.Any<Uri>())
            .Returns(UrlInfo.AsUrl("http://localhost/media/a/summer.png?v=1", "test", null));

        LiveSnapshot snapshot = await _builder.BuildAsync([Zone(1, "home-top")], [Published(10, MediaA, 1)]);

        Assert.That(snapshot.Zones["home-top"].Candidates.Single().ImageUrl, Is.EqualTo("/media/a/summer.png?v=1"));
    }

    [Test]
    public async Task AnAbsoluteAddressOnAnotherHost_IsKept()
    {
        _urlProvider.GetMediaUrl(Arg.Any<IPublishedContent>(), "umbracoFile", UrlMode.Relative, Arg.Any<string?>(), Arg.Any<Uri>())
            .Returns(UrlInfo.AsUrl("http://localhost:8080/media/a.png", "test", null));

        LiveSnapshot snapshot = await _builder.BuildAsync([Zone(1, "home-top")], [Published(10, MediaA, 1)]);

        Assert.That(snapshot.Zones["home-top"].Candidates.Single().ImageUrl, Is.EqualTo("http://localhost:8080/media/a.png"));
    }

    private static Banner PublishedHtml(int id, string html, params int[] zoneIds) => new()
    {
        Id = id,
        Key = Guid.NewGuid(),
        Name = "Html banner " + id,
        Type = BannerType.Html,
        IsPublished = true,
        Live = new BannerVersion
        {
            Kind = BannerVersionKind.Live,
            Html = html,
            Weight = 3,
            ZoneIds = [.. zoneIds],
        },
    };

    [Test]
    public async Task AnHtmlBanner_IsACandidate_WithNoMediaLookup_AndIsNotImageMissing()
    {
        LiveSnapshot snapshot = await _builder.BuildAsync([Zone(1, "home-top", 728, 90)], [PublishedHtml(20, "<p>Invented html</p>", 1)]);

        SnapshotBanner candidate = snapshot.Zones["home-top"].Candidates.Single();
        Assert.Multiple(() =>
        {
            Assert.That(candidate.BannerId, Is.EqualTo(20));
            Assert.That(candidate.Html, Is.EqualTo("<p>Invented html</p>"));
            Assert.That(candidate.Weight, Is.EqualTo(3));
            Assert.That(candidate.Selected.Html, Is.EqualTo("<p>Invented html</p>"));
            Assert.That(snapshot.ImageMissingBannerIds, Is.Empty);
            Assert.That(snapshot.MediaKeys, Is.Empty);
        });
        await _mediaCache.DidNotReceiveWithAnyArgs().GetByIdAsync(default(Guid));
        _images.DidNotReceiveWithAnyArgs().GetImageUrl(default!);
    }

    [Test]
    public async Task AnHtmlBanner_AndAnImageBanner_ShareAZone()
    {
        LiveSnapshot snapshot = await _builder.BuildAsync([Zone(1, "home-top")], [Published(10, MediaA, 1), PublishedHtml(20, "<p>x</p>", 1)]);

        Assert.That(snapshot.Zones["home-top"].Candidates.Select(c => c.BannerId), Is.EquivalentTo(new[] { 10, 20 }));
        Assert.That(snapshot.Zones["home-top"].Candidates.Single(c => c.BannerId == 10).Html, Is.Null);
    }
}
