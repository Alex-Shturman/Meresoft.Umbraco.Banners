using Umbraco.Cms.Core.DependencyInjection;

namespace Meresoft.Umbraco.Banners.Composing;

/// <summary>Registers the package with Umbraco. Called by <see cref="BannersComposer"/>.</summary>
public static class BannersBuilderExtensions
{
    /// <summary>Adds Meresoft Banners to the Umbraco builder.</summary>
    public static IUmbracoBuilder AddMeresoftBanners(this IUmbracoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder;
    }
}
