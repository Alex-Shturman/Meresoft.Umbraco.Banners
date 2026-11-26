using System.Reflection;
using Meresoft.Umbraco.Banners.Caching;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Caching;

/// <summary>
/// SnapshotBanner has a hand written copy constructor (it is what <c>with</c> uses). This guards against a property
/// being added to the record and forgotten there.
/// </summary>
[TestFixture]
public class SnapshotBannerCopyTests
{
    [Test]
    public void AWithCopy_KeepsEveryPublicInstanceProperty()
    {
        SnapshotBanner original = new()
        {
            BannerId = 0,
            ZoneId = 0,
            ImageUrl = string.Empty,
            AltText = string.Empty,
            LinkUrl = string.Empty,
        };
        PropertyInfo[] properties = typeof(SnapshotBanner)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.CanWrite && p.SetMethod!.IsPublic)
            .ToArray();
        Assert.That(properties, Is.Not.Empty);

        int seed = 1;
        foreach (PropertyInfo property in properties)
        {
            property.SetValue(original, NonDefaultValue(property.PropertyType, seed++));
        }

        SnapshotBanner copy = original with { };

        Assert.Multiple(() =>
        {
            foreach (PropertyInfo property in properties)
            {
                object? expected = property.GetValue(original);
                Assert.That(expected, Is.Not.EqualTo(DefaultOf(property.PropertyType)), $"{property.Name}: the test must set a non default value");
                Assert.That(property.GetValue(copy), Is.EqualTo(expected), $"{property.Name} was not copied by the copy constructor");
            }
        });
    }

    private static object NonDefaultValue(Type type, int seed)
    {
        Type target = Nullable.GetUnderlyingType(type) ?? type;
        if (target == typeof(string))
        {
            return "value " + seed;
        }

        if (target == typeof(int))
        {
            return seed + 10;
        }

        if (target == typeof(long))
        {
            return (long)seed + 10;
        }

        if (target == typeof(bool))
        {
            return true;
        }

        if (target == typeof(DateTime))
        {
            return new DateTime(2027, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddDays(seed);
        }

        throw new NotSupportedException($"Teach this test a non default value for {type}.");
    }

    private static object? DefaultOf(Type type) => type.IsValueType && Nullable.GetUnderlyingType(type) is null ? Activator.CreateInstance(type) : null;
}
