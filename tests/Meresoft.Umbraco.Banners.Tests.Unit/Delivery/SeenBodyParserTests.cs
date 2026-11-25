using Meresoft.Umbraco.Banners.Delivery;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Delivery;

[TestFixture]
public class SeenBodyParserTests
{
    [Test]
    public void EachLineIsATokenAndABannerId()
    {
        IReadOnlyList<SeenLine> lines = SeenBodyParser.Parse("tokenA 12\ntokenB 15");

        Assert.That(lines, Is.EqualTo(new[] { new SeenLine("tokenA", 12), new SeenLine("tokenB", 15) }));
    }

    [Test]
    public void WindowsLineEndingsAndATrailingNewline_AreAccepted()
    {
        IReadOnlyList<SeenLine> lines = SeenBodyParser.Parse("tokenA 12\r\ntokenB 15\r\n");

        Assert.That(lines, Has.Count.EqualTo(2));
    }

    [Test]
    public void AtMostFiftyLines_AreRead_ExtraLinesAreIgnored()
    {
        string body = string.Join('\n', Enumerable.Range(1, 80).Select(i => $"tok{i} {i}"));

        IReadOnlyList<SeenLine> lines = SeenBodyParser.Parse(body);

        Assert.That(lines, Has.Count.EqualTo(SeenBodyParser.MaxLines));
        Assert.That(lines[^1], Is.EqualTo(new SeenLine("tok50", 50)));
    }

    [TestCase("")]
    [TestCase("   ")]
    [TestCase("onlytoken")]
    [TestCase("token notanumber")]
    [TestCase("token 12.5")]
    [TestCase("token -3")]
    [TestCase("token +3")]
    [TestCase("token 99999999999")]
    [TestCase("token 12 extra")]
    [TestCase(" 12")]
    [TestCase("token  12")]
    public void MalformedLines_AreIgnored(string line)
    {
        Assert.That(SeenBodyParser.Parse(line), Is.Empty);
    }

    [Test]
    public void AMalformedLineDoesNotStopTheValidOnesAroundIt()
    {
        IReadOnlyList<SeenLine> lines = SeenBodyParser.Parse("tokenA 1\ngarbage\n\ntokenB 2");

        Assert.That(lines.Select(l => l.BannerId), Is.EqualTo(new[] { 1, 2 }));
    }

    [Test]
    public void ANullBody_GivesNoLines()
    {
        Assert.That(SeenBodyParser.Parse(null), Is.Empty);
    }
}
