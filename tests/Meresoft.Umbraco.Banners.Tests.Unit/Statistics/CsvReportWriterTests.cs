using Meresoft.Umbraco.Banners.Statistics;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Statistics;

[TestFixture]
public class CsvReportWriterTests
{
    private static readonly DateOnly Day = new(2027, 3, 1);

    private static string[] Lines(params CsvReportRow[] rows) =>
        CsvReportWriter.Write(rows).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

    [Test]
    public void TheHeaderIsDateZoneBannerImpressionsClicks() =>
        Assert.That(Lines()[0], Is.EqualTo("date,zone,banner,impressions,clicks"));

    [Test]
    public void OneRowPerBannerPerZonePerDay()
    {
        string[] lines = Lines(
            new CsvReportRow(Day, "home-top", "Summer promo", 100, 3),
            new CsvReportRow(Day.AddDays(1), "home-top", "Summer promo", 80, 1),
            new CsvReportRow(Day, "sidebar", "Summer promo", 5, 0));

        Assert.That(lines, Is.EqualTo(new[]
        {
            "date,zone,banner,impressions,clicks",
            "2027-03-01,home-top,Summer promo,100,3",
            "2027-03-02,home-top,Summer promo,80,1",
            "2027-03-01,sidebar,Summer promo,5,0",
        }));
    }

    [TestCase("=1+1")]
    [TestCase("+cmd")]
    [TestCase("-2")]
    [TestCase("@SUM(A1)")]
    public void ValuesThatStartLikeAFormula_GetALeadingApostrophe(string name)
    {
        string[] lines = Lines(new CsvReportRow(Day, "z", name, 1, 0));

        Assert.That(lines[1], Is.EqualTo($"2027-03-01,z,'{name},1,0"));
    }

    [Test]
    public void TheZoneColumnIsProtectedToo() =>
        Assert.That(Lines(new CsvReportRow(Day, "=evil", "b", 1, 0))[1], Does.StartWith("2027-03-01,'=evil,"));

    [Test]
    public void DeletedBannersAndZones_GetPlaceholderNames()
    {
        string[] lines = Lines(new CsvReportRow(Day, null, null, 1, 0));

        Assert.That(lines[1], Is.EqualTo("2027-03-01,(deleted zone),(deleted banner),1,0"));
    }

    [Test]
    public void CommasQuotesAndLineBreaks_AreQuoted()
    {
        string csv = CsvReportWriter.Write([new CsvReportRow(Day, "z", "Say \"hi\", now\nplease", 1, 0)]);

        Assert.That(csv, Does.Contain("\"Say \"\"hi\"\", now\nplease\""));
    }

    [Test]
    public void AFormulaValueThatNeedsQuotes_IsPrefixedInsideTheQuotes()
    {
        string csv = CsvReportWriter.Write([new CsvReportRow(Day, "z", "=a,b", 1, 0)]);

        Assert.That(csv, Does.Contain("\"'=a,b\""));
    }
}
