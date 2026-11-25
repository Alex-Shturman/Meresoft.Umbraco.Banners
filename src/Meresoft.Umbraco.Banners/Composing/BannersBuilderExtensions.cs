using Meresoft.Umbraco.Banners.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.DependencyInjection;

namespace Meresoft.Umbraco.Banners.Composing;

/// <summary>Registers the package with Umbraco. Called by <see cref="BannersComposer"/>.</summary>
public static class BannersBuilderExtensions
{
    /// <summary>Adds Meresoft Banners to the Umbraco builder.</summary>
    public static IUmbracoBuilder AddMeresoftBanners(this IUmbracoBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<BannersOptions>()
            .Bind(builder.Config.GetSection(BannersOptions.SectionName))
            .ValidateOnStart();
        builder.Services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<BannersOptions>, BannersOptionsValidator>());

        return builder;
    }
}
