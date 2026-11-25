using Meresoft.Umbraco.Banners.Persistence.Repositories;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Persistence;

/// <summary>
/// The Day column is a date. SQLite stores the DateOnly of the table mapping as text, SQL Server needs a date value
/// without relying on driver support for DateOnly; both must hold a date with no time part.
/// </summary>
[TestFixture]
public class StatisticsDayParameterTests
{
    private static readonly DateOnly Day = new(2027, 3, 1);

    [Test]
    public void Sqlite_GetsTheIsoDateText_ThatTheTableMappingStores()
    {
        object value = StatisticsDayParameter.For(isSqlite: true, Day);

        Assert.That(value, Is.EqualTo("2027-03-01"));
    }

    [Test]
    public void SqlServer_GetsMidnightOfTheDay_AsADateTime()
    {
        object value = StatisticsDayParameter.For(isSqlite: false, Day);

        Assert.That(value, Is.EqualTo(new DateTime(2027, 3, 1, 0, 0, 0, DateTimeKind.Unspecified)));
        Assert.That(((DateTime)value).TimeOfDay, Is.EqualTo(TimeSpan.Zero), "a date has no time part");
    }

    [Test]
    public void TheTextIsCultureIndependent()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new System.Globalization.CultureInfo("ar-SA");

            Assert.That(StatisticsDayParameter.For(isSqlite: true, Day), Is.EqualTo("2027-03-01"));
        }
        finally
        {
            System.Globalization.CultureInfo.CurrentCulture = previous;
        }
    }
}
