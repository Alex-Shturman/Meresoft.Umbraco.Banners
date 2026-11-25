using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.ManagementApi.Authorization;
using Meresoft.Umbraco.Banners.ManagementApi.OpenApi;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Services.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;
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

        builder.Services.AddSingleton<IZoneRepository, ZoneRepository>();
        builder.Services.AddSingleton<IBannerRepository, BannerRepository>();

        builder.Services.AddTransient<BannerAuditWriter>();
        builder.Services.AddSingleton<IAuthorizationHandler, SectionAccessHandler>();
        builder.Services.AddAuthorizationBuilder().AddPolicy(BannersConstants.SectionAccessPolicy, policy =>
        {
            policy.AuthenticationSchemes.Add(OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme);
            policy.RequireAuthenticatedUser();
            policy.Requirements.Add(new SectionAccessRequirement());
        });
        builder.Services.ConfigureOptions<BannersSwaggerConfiguration>();

        return builder;
    }
}
