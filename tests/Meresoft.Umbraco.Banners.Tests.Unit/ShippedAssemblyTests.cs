using System.Reflection;
using System.Runtime.CompilerServices;
using Meresoft.Umbraco.Banners.Composing;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Unit;

/// <summary>The shipped assembly must not open its internals to a mocking library.</summary>
[TestFixture]
public class ShippedAssemblyTests
{
    [Test]
    public void TheShippedAssembly_DoesNotExposeItsInternalsToTheProxyGenerator()
    {
        string[] friends = typeof(BannersComposer).Assembly
            .GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(a => a.AssemblyName)
            .ToArray();

        Assert.That(friends, Has.None.Contains("DynamicProxyGenAssembly2"));
        Assert.That(friends, Is.Not.Empty, "the test assemblies keep their access");
    }
}
