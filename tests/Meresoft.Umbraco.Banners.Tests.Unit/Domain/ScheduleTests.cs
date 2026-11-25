using Meresoft.Umbraco.Banners.Domain;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Domain;

[TestFixture]
public class ScheduleTests
{
    private static readonly DateTime Start = new(2027, 6, 1, 8, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime End = new(2027, 6, 10, 8, 0, 0, DateTimeKind.Utc);

    [Test]
    public void HasStarted_StartIsInclusive()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Schedule.HasStarted(Start, Start.AddTicks(-1)), Is.False);
            Assert.That(Schedule.HasStarted(Start, Start), Is.True);
            Assert.That(Schedule.HasStarted(Start, Start.AddTicks(1)), Is.True);
            Assert.That(Schedule.HasStarted(null, DateTime.MinValue), Is.True, "no start is an open start");
        });
    }

    [Test]
    public void HasEnded_EndIsExclusive()
    {
        Assert.Multiple(() =>
        {
            Assert.That(Schedule.HasEnded(End, End.AddTicks(-1)), Is.False);
            Assert.That(Schedule.HasEnded(End, End), Is.True);
            Assert.That(Schedule.HasEnded(End, End.AddTicks(1)), Is.True);
            Assert.That(Schedule.HasEnded(null, DateTime.MaxValue), Is.False, "no end is an open end");
        });
    }

    [TestCase(-1, false)]
    [TestCase(0, true)]
    public void IsActive_AroundTheStart(int ticks, bool expected) =>
        Assert.That(Schedule.IsActive(Start, End, Start.AddTicks(ticks)), Is.EqualTo(expected));

    [TestCase(-1, true)]
    [TestCase(0, false)]
    public void IsActive_AroundTheEnd(int ticks, bool expected) =>
        Assert.That(Schedule.IsActive(Start, End, End.AddTicks(ticks)), Is.EqualTo(expected));

    [Test]
    public void IsActive_WithOpenEnds_IsAlwaysTrue() =>
        Assert.That(Schedule.IsActive(null, null, new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc)), Is.True);
}
