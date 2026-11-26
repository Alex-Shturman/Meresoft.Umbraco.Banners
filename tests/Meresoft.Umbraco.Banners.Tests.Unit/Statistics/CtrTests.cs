using Meresoft.Umbraco.Banners.Statistics;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Statistics;

[TestFixture]
public class CtrTests
{
    [Test]
    public void OneHundredImpressionsAndThreeClicks_Give3Point0Percent() =>
        Assert.That(Ctr.Calculate(100, 3), Is.EqualTo(3.0m));

    [TestCase(0, 0)]
    [TestCase(0, 5)]
    public void NoImpressions_GiveNull(long impressions, long clicks) =>
        Assert.That(Ctr.Calculate(impressions, clicks), Is.Null);

    [TestCase(3, 1, 33.3)]
    [TestCase(3, 2, 66.7)]
    [TestCase(8, 1, 12.5)]
    [TestCase(1_000, 1, 0.1)]
    [TestCase(1_000_000, 1, 0.0)]
    public void TheValueIsRoundedToOneDecimalPercent(long impressions, long clicks, double expected) =>
        Assert.That(Ctr.Calculate(impressions, clicks), Is.EqualTo((decimal)expected));
}
