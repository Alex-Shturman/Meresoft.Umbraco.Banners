using System.Reflection;
using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Configuration;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Delivery;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.ManagementApi.Authorization;
using Meresoft.Umbraco.Banners.ManagementApi.Banners;
using Meresoft.Umbraco.Banners.ManagementApi.OpenApi;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Rendering;
using Meresoft.Umbraco.Banners.Selection;
using Meresoft.Umbraco.Banners.Services;
using Meresoft.Umbraco.Banners.Services.Audit;
using Meresoft.Umbraco.Banners.Tokens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using OpenIddict.Validation.AspNetCore;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Extensions;

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

        builder.Services.TryAddSingleton(TimeProvider.System);

        // A class library that only references the shared framework is not scanned for controllers,
        // so the management API controllers, the tag helper and the view component are registered here.
        Assembly packageAssembly = typeof(BannersConstants).Assembly;
        builder.Services.AddMvcCore().ConfigureApplicationPartManager(manager =>
        {
            if (!manager.ApplicationParts.OfType<AssemblyPart>().Any(part => part.Assembly == packageAssembly))
            {
                manager.ApplicationParts.Add(new AssemblyPart(packageAssembly));
            }
        });

        builder.Services.AddSingleton<IZoneRepository, ZoneRepository>();
        builder.Services.AddSingleton<IBannerRepository, BannerRepository>();
        builder.Services.AddSingleton<IRequestedZoneRepository, RequestedZoneRepository>();
        builder.Services.AddSingleton<IStatisticsRepository, StatisticsRepository>();

        builder.Services.AddSingleton<BannerStateCalculator>();
        builder.Services.AddSingleton<IBannerZoneService, BannerZoneService>();
        builder.Services.AddSingleton<IBannerService, BannerService>();
        builder.Services.AddSingleton<IBannerStatisticsService, BannerStatisticsService>();

        // Impressions and clicks (US3): view tokens, bot filter, in-memory counters and the public endpoints.
        builder.Services.AddSingleton<IViewTokenProtector, ViewTokenProtector>();
        builder.Services.AddSingleton<ReplaySet>();
        builder.Services.AddSingleton<BotMatcher>();
        builder.Services.AddSingleton<CounterBuffer>();
        builder.Services.AddSingleton<CounterFlusher>();
        builder.Services.AddSingleton<IBannerLinkLookup, BannerLinkLookup>();
        builder.Services.AddOptions<MvcOptions>().Configure<IOptions<BannersOptions>>(
            (mvc, options) => mvc.Conventions.Add(new PublicPathRouteConvention(options.Value.PublicPath)));

        builder.Services.AddSingleton<LiveSnapshotBuilder>();
        builder.Services.AddSingleton<ILiveSnapshotAccessor, LiveSnapshotAccessor>();
        builder.Services.AddSingleton<IRandomSource, SharedRandomSource>();
        builder.Services.AddSingleton<WeightedSampler>();
        builder.Services.AddSingleton<ScheduleFilter>();
        builder.Services.AddSingleton<IBannerSelector, BannerSelector>();
        builder.Services.AddSingleton<RequestedZoneRecorder>();
        builder.Services.AddSingleton<BannerZoneHtmlWriter>();
        builder.Services.AddSingleton<BannerZoneRenderer>();
        builder.Services.AddSingleton<BannerResponseMapper>();

        builder
            .AddNotificationHandler<BannersChangedNotification, BannersChangedNotificationHandler>()
            .AddNotificationAsyncHandler<BannersCacheRefresherNotification, SnapshotRebuildHandler>()
            .AddNotificationAsyncHandler<MediaCacheRefresherNotification, MediaChangedHandler>()
            .AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, SnapshotStartupHandler>()
            .AddNotificationHandler<UmbracoApplicationStoppingNotification, FlushOnStoppingHandler>();
        builder.Services.AddRecurringBackgroundJob<SnapshotSafetyReloadJob>();
        builder.Services.AddRecurringBackgroundJob<BannersFlushJob>();

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
