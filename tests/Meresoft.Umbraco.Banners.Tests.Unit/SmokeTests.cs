using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit;

[TestFixture]
public class SmokeTests
{
    [Test]
    public void TestProjectRuns()
    {
        Assert.That(typeof(SmokeTests).Assembly, Is.Not.Null);
    }
}
