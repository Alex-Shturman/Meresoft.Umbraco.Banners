using Meresoft.Umbraco.Banners.Domain;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Domain;

[TestFixture]
public class UtcTimestampTests
{
    [Test]
    public void Truncate_RoundsDownToWholeTenMilliseconds()
    {
        var value = new DateTime(2027, 5, 10, 12, 30, 45, 987, DateTimeKind.Utc).AddTicks(4321);

        DateTime result = UtcTimestamp.Truncate(value);

        Assert.That(result, Is.EqualTo(new DateTime(2027, 5, 10, 12, 30, 45, 980, DateTimeKind.Utc)));
    }

    [TestCase(0)]
    [TestCase(3)]
    [TestCase(7)]
    [TestCase(10)]
    public void Truncate_KeepsValuesThatAreAlreadyAtTheResolution(int milliseconds)
    {
        // SQL Server datetime stores .000, .003 and .007 steps; multiples of 10 ms survive a round trip exactly.
        var value = new DateTime(2027, 5, 10, 12, 30, 45, DateTimeKind.Utc).AddMilliseconds(milliseconds / 10 * 10);

        Assert.That(UtcTimestamp.Truncate(value), Is.EqualTo(value));
    }

    [Test]
    public void Truncate_ResultIsUtc()
    {
        Assert.Multiple(() =>
        {
            Assert.That(UtcTimestamp.Truncate(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Unspecified)).Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(UtcTimestamp.Truncate(new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Local)).Kind, Is.EqualTo(DateTimeKind.Utc));
        });
    }

    [Test]
    public void Truncate_ConvertsLocalTimeToUtc()
    {
        var local = new DateTime(2027, 6, 1, 12, 0, 0, DateTimeKind.Local);

        Assert.That(UtcTimestamp.Truncate(local), Is.EqualTo(local.ToUniversalTime()));
    }

    [Test]
    public void Truncate_Nullable_PassesNullThrough()
    {
        Assert.That(UtcTimestamp.Truncate((DateTime?)null), Is.Null);
    }

    [Test]
    public void SaveAfterPublish_StaysAfterOnceTruncated()
    {
        DateTime published = UtcTimestamp.Truncate(new DateTime(2027, 5, 10, 12, 0, 0, 5, DateTimeKind.Utc));
        DateTime savedLater = UtcTimestamp.Truncate(published.AddMilliseconds(40));

        Assert.That(savedLater, Is.GreaterThan(published));
    }
}
