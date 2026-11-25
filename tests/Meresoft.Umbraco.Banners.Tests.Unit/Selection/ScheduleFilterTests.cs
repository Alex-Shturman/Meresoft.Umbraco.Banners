using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Selection;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Selection;

[TestFixture]
public class ScheduleFilterTests
{
    private static readonly DateTime Start = new(2027, 6, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2027, 6, 10, 8, 0, 0, DateTimeKind.Utc);

    private FakeTimeProvider _time = null!;
    private ScheduleFilter _filter = null!;

    [SetUp]
    public void SetUp()
    {
        _time = new FakeTimeProvider(new DateTimeOffset(Start.AddDays(1)));
        _filter = new ScheduleFilter(_time);
    }

    private static SnapshotBanner Banner(int id, DateTime? start, DateTime? end) => new()
    {
        BannerId = id,
        ZoneId = 1,
        ImageUrl = "/m/x.png",
        AltText = "alt",
        LinkUrl = "https://example.org/x",
        StartUtc = start,
        EndUtc = end,
    };

    private void At(DateTime utc) => _time.SetUtcNow(new DateTimeOffset(utc));

    [Test]
    public void InsideTheWindow_IsActive() => Assert.That(_filter.IsActive(Banner(1, Start, End)), Is.True);

    [Test]
    public void AtStart_IsActive_BeforeStart_IsNot()
    {
        At(Start);
        Assert.That(_filter.IsActive(Banner(1, Start, End)), Is.True, "start is inclusive");

        At(Start.AddTicks(-1));
        Assert.That(_filter.IsActive(Banner(1, Start, End)), Is.False);
    }

    [Test]
    public void AtEnd_IsNotActive_JustBeforeEnd_IsActive()
    {
        At(End.AddTicks(-1));
        Assert.That(_filter.IsActive(Banner(1, Start, End)), Is.True);

        At(End);
        Assert.That(_filter.IsActive(Banner(1, Start, End)), Is.False, "end is exclusive");
    }

    [Test]
    public void OpenEnds_AreAlwaysOpen()
    {
        At(new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.That(_filter.IsActive(Banner(1, null, null)), Is.True);
        Assert.That(_filter.IsActive(Banner(2, Start, null)), Is.True);

        At(new DateTime(2001, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.That(_filter.IsActive(Banner(3, null, End)), Is.True);
    }

    [Test]
    public void Active_DropsFutureAndExpiredBanners_AndKeepsTheOrder()
    {
        SnapshotBanner[] all =
        [
            Banner(1, null, null),
            Banner(2, End.AddDays(1), null),
            Banner(3, null, Start.AddDays(-1)),
            Banner(4, Start, End),
        ];

        Assert.That(_filter.Active(all).Select(b => b.BannerId), Is.EqualTo(new[] { 1, 4 }));
    }

    [Test]
    public void Active_WhenEveryBannerIsActive_GivesTheSameListWithoutCopying()
    {
        SnapshotBanner[] all = [Banner(1, null, null), Banner(2, Start, End)];

        Assert.That(_filter.Active(all), Is.SameAs(all));
    }
}
