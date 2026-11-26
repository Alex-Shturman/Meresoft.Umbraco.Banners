using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Tokens;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Tokens;

[TestFixture]
public class ViewTokenProtectorTests
{
    private static ViewTokenProtector Create(IDataProtectionProvider? provider = null, TimeSpan? lifetime = null) =>
        new(provider ?? new EphemeralDataProtectionProvider(), Options.Create(new BannersOptions { ViewTokenLifetime = lifetime ?? TimeSpan.FromMinutes(10) }));

    [Test]
    public void RoundTrip_GivesBackFormatVersionZoneBannersAndA96BitNonce()
    {
        ViewTokenProtector protector = Create();

        string token = protector.Issue(7, [12, 15, 21]);

        Assert.That(protector.TryValidate(token, out ViewTokenPayload? payload), Is.True);
        Assert.That(payload!.Version, Is.EqualTo(ViewTokenPayload.CurrentVersion));
        Assert.That(payload.ZoneId, Is.EqualTo(7));
        Assert.That(payload.BannerIds, Is.EqualTo(new[] { 12, 15, 21 }));
        Assert.That(payload.Nonce, Has.Length.EqualTo(12));
    }

    [Test]
    public void TheTokenIsUrlSafeText()
    {
        string token = Create().Issue(3, [1]);

        Assert.That(token, Does.Match("^[A-Za-z0-9_-]+$"));
    }

    [Test]
    public void AChangedCharacter_FailsValidation()
    {
        ViewTokenProtector protector = Create();
        string token = protector.Issue(7, [12]);
        int middle = token.Length / 2;
        char replacement = token[middle] == 'A' ? 'B' : 'A';
        string tampered = token[..middle] + replacement + token[(middle + 1)..];

        Assert.That(protector.TryValidate(tampered, out ViewTokenPayload? payload), Is.False);
        Assert.That(payload, Is.Null);
    }

    [Test]
    public void ATokenFromAnotherKeyRing_FailsValidation()
    {
        string token = Create().Issue(7, [12]);

        Assert.That(Create().TryValidate(token, out _), Is.False);
    }

    [Test]
    public void AnExpiredToken_FailsValidation()
    {
        ViewTokenProtector protector = Create(lifetime: TimeSpan.FromMilliseconds(1));
        string token = protector.Issue(7, [12]);

        Thread.Sleep(150);

        Assert.That(protector.TryValidate(token, out _), Is.False);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("not a token")]
    [TestCase("AAAA")]
    public void NothingOrGarbage_FailsValidationWithoutThrowing(string? token)
    {
        Assert.That(Create().TryValidate(token, out _), Is.False);
    }

    [Test]
    public void TwoTokensNeverShareANonce()
    {
        ViewTokenProtector protector = Create();
        var nonces = new HashSet<string>();

        for (int i = 0; i < 2_000; i++)
        {
            Assert.That(protector.TryValidate(protector.Issue(1, [1]), out ViewTokenPayload? payload), Is.True);
            Assert.That(nonces.Add(Convert.ToHexString(payload!.Nonce)), Is.True, "a nonce repeated");
        }
    }

    [Test]
    public void ThePayloadHoldsNoVisitorData()
    {
        string[] names = typeof(ViewTokenPayload).GetProperties().Select(p => p.Name).Order().ToArray();

        Assert.That(names, Is.EqualTo(new[] { "BannerIds", "Nonce", "Version", "ZoneId" }));
    }
}
