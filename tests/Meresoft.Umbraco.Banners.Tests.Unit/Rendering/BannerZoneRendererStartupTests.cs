using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Rendering;
using Meresoft.Umbraco.Banners.Selection;
using Meresoft.Umbraco.Banners.Tokens;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Rendering;

/// <summary>Before the first snapshot build a zone must not be answered as unknown and cached that way.</summary>
[TestFixture]
public class BannerZoneRendererStartupTests
{
    private ILiveSnapshotAccessor _accessor = null!;
    private RequestedZoneRecorder _recorder = null!;
    private BannerZoneRenderer _renderer = null!;
    private bool _built;

    [SetUp]
    public void SetUp()
    {
        _built = false;
        var zone = new SnapshotZone(1, "home-top", 1, ZoneRenderMode.Client, null, null, []);
        var snapshot = new LiveSnapshot(new Dictionary<string, SnapshotZone> { ["home-top"] = zone }, new HashSet<int>(), new HashSet<Guid>());
        _accessor = Substitute.For<ILiveSnapshotAccessor>();
        _accessor.IsBuilt.Returns(_ => _built);
        _accessor.Current.Returns(snapshot);
        _recorder = new RequestedZoneRecorder(TimeProvider.System);
        IOptions<BannersOptions> options = Options.Create(new BannersOptions());
        var selector = Substitute.For<IBannerSelector>();
        _renderer = new BannerZoneRenderer(
            selector,
            _accessor,
            _recorder,
            new BannerZoneHtmlWriter(options, new ViewTokenProtector(new EphemeralDataProtectionProvider(), options)));
    }

    private static DefaultHttpContext Context(bool headersStarted = false)
    {
        var context = new DefaultHttpContext();
        if (headersStarted)
        {
            context.Features.Set<IHttpResponseFeature>(new StartedResponseFeature());
        }

        return context;
    }

    [Test]
    public async Task WhenTheBuildFinishesWithinTheWait_TheZoneIsAnsweredFromTheSnapshot_AndTheResponseIsNotMarked()
    {
        _accessor.WaitForFirstBuildAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            _built = true;
            return Task.FromResult(true);
        });
        DefaultHttpContext context = Context();

        ZoneSelection selection = await _renderer.SelectAsync("home-top", context);

        Assert.That(selection.Mode, Is.EqualTo(ZoneRenderMode.Client));
        Assert.That(context.Response.Headers.CacheControl.ToString(), Is.Empty);
    }

    [Test]
    public async Task TheWaitIsBoundedByTwoSeconds()
    {
        _accessor.WaitForFirstBuildAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));

        await _renderer.SelectAsync("home-top", Context());

        await _accessor.Received(1).WaitForFirstBuildAsync(TimeSpan.FromSeconds(2), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task WhenStillNotBuilt_TheResponseIsNoStore_AndTheZoneIsNotNotedAsRequested()
    {
        _accessor.WaitForFirstBuildAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        DefaultHttpContext context = Context();

        ZoneSelection selection = await _renderer.SelectAsync("home-top", context);

        Assert.That(selection.Banners, Is.Empty);
        Assert.That(context.Response.Headers.CacheControl.ToString(), Is.EqualTo("no-store"));
        Assert.That(_recorder.Snapshot(), Is.Empty, "an unbuilt snapshot says nothing about which zones exist");
    }

    [Test]
    public async Task WhenTheHeadersHaveStarted_NothingIsSet_AndNothingThrows()
    {
        _accessor.WaitForFirstBuildAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        DefaultHttpContext context = Context(headersStarted: true);

        Assert.That(async () => await _renderer.SelectAsync("home-top", context), Throws.Nothing);
        Assert.That(context.Response.Headers.CacheControl.ToString(), Is.Empty);
    }

    [Test]
    public async Task ASecondZoneOfTheSameRequest_DoesNotWaitAgainAfterATimeout()
    {
        _accessor.WaitForFirstBuildAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>()).Returns(Task.FromResult(false));
        DefaultHttpContext context = Context();

        await _renderer.SelectAsync("home-top", context);
        await _renderer.SelectAsync("home-top", context);
        await _renderer.SelectAsync("other", context);

        await _accessor.Received(1).WaitForFirstBuildAsync(Arg.Any<TimeSpan>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task WhenBuilt_NoWaitHappens()
    {
        _built = true;

        await _renderer.SelectAsync("home-top", Context());

        await _accessor.DidNotReceiveWithAnyArgs().WaitForFirstBuildAsync(default, default);
    }

    private sealed class StartedResponseFeature : HttpResponseFeature
    {
        public override bool HasStarted => true;
    }
}
