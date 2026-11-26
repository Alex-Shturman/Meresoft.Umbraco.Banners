using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Domain;
using Microsoft.Extensions.Time.Testing;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Counting;

[TestFixture]
public class RequestedZoneRecorderTests
{
    [Test]
    public void APublicCallerMayPassAnAliasThatIsNotNormalised()
    {
        var recorder = new RequestedZoneRecorder(new FakeTimeProvider());

        recorder.Record("  Brand-New ");

        Assert.That(recorder.Snapshot().Select(r => r.Alias), Is.EqualTo(new[] { "brand-new" }));
    }

    [Test]
    public void InvalidAliasesAreIgnored()
    {
        var recorder = new RequestedZoneRecorder(new FakeTimeProvider());

        recorder.Record("9 bad alias");
        recorder.Record("");

        Assert.That(recorder.Snapshot(), Is.Empty);
    }

    [Test]
    public void NormalisingAnAliasThatIsAlreadyNormalised_ReturnsTheSameInstance()
    {
        string alias = new string("home-top".AsSpan());

        Assert.That(ReferenceEquals(alias, AliasNormalizer.Normalize(alias)), Is.True, "so the render path pays nothing for the check");
    }
}
