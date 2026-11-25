using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using Umbraco.Extensions;

namespace Meresoft.Umbraco.Banners.Sample;

/// <summary>Registers the seeder that creates the invented sample pages on first start.</summary>
public sealed class SampleContentComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder) =>
        builder.AddNotificationAsyncHandler<UmbracoApplicationStartedNotification, SampleContentSeeder>();
}

/// <summary>
/// Also creates one invented image media item for banners. Creates three document types with templates and one published page each, when missing:
/// home (zone home-top), long (zone long-bottom below the fold), cached (zone cached-top, output
/// cached for 60 s). Everything here is invented sample data.
/// </summary>
public sealed class SampleContentSeeder : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private static readonly (string Alias, string Name, string Url)[] Pages =
    [
        ("homePage", "Home", "home"),
        ("longPage", "Long", "long"),
        ("cachedPage", "Cached", "cached"),
    ];

    private readonly IContentTypeService _contentTypeService;
    private readonly ITemplateService _templateService;
    private readonly IContentService _contentService;
    private readonly IMediaService _mediaService;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly IWebHostEnvironment _hostingEnvironment;
    private readonly IRuntimeState _runtimeState;
    private readonly ILogger<SampleContentSeeder> _logger;

    public SampleContentSeeder(
        IContentTypeService contentTypeService,
        ITemplateService templateService,
        IContentService contentService,
        IMediaService mediaService,
        IShortStringHelper shortStringHelper,
        IWebHostEnvironment hostingEnvironment,
        IRuntimeState runtimeState,
        ILogger<SampleContentSeeder> logger)
    {
        _contentTypeService = contentTypeService;
        _templateService = templateService;
        _contentService = contentService;
        _mediaService = mediaService;
        _shortStringHelper = shortStringHelper;
        _hostingEnvironment = hostingEnvironment;
        _runtimeState = runtimeState;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        if (_runtimeState.Level != RuntimeLevel.Run)
        {
            return;
        }

        await SeedImageAsync(cancellationToken);

        foreach ((string alias, string name, string _) in Pages)
        {
            if (_contentTypeService.Get(alias) is not null)
            {
                continue;
            }

            string viewPath = Path.Combine(Path.Combine(_hostingEnvironment.ContentRootPath, "Views"), alias + ".cshtml");
            string? markup = System.IO.File.Exists(viewPath) ? await System.IO.File.ReadAllTextAsync(viewPath, cancellationToken) : null;

            ITemplate? template = (await _templateService.CreateAsync(name, alias, markup, Constants.Security.SuperUserKey)).Result;
            if (template is null)
            {
                _logger.LogWarning("Sample template {Alias} could not be created.", alias);
                continue;
            }

            var contentType = new ContentType(_shortStringHelper, Constants.System.Root)
            {
                Alias = alias,
                Name = name + " page",
                AllowedAsRoot = true,
                Icon = "icon-document",
            };
            contentType.AllowedTemplates = [template];
            contentType.SetDefaultTemplate(template);
            await _contentTypeService.CreateAsync(contentType, Constants.Security.SuperUserKey);

            IContent content = _contentService.Create(name, Constants.System.Root, alias);
            _contentService.Save(content);
            _contentService.Publish(content, ["*"]);
        }
    }
    /// <summary>Name of the invented image that banners in the sample site and the browser tests use.</summary>
    public const string ImageName = "Invented sample banner image";

    // A valid 1 x 1 PNG; the zone width and height scale it.
    private const string TinyPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==";

    private async Task SeedImageAsync(CancellationToken cancellationToken)
    {
        if (_mediaService.GetRootMedia().Any(m => m.Name == ImageName))
        {
            return;
        }

        string folder = Path.Combine(_hostingEnvironment.WebRootPath, "media", "sample");
        Directory.CreateDirectory(folder);
        await System.IO.File.WriteAllBytesAsync(Path.Combine(folder, "banner.png"), Convert.FromBase64String(TinyPngBase64), cancellationToken);

        IMedia image = _mediaService.CreateMedia(ImageName, Constants.System.Root, Constants.Conventions.MediaTypes.Image);
        image.SetValue(Constants.Conventions.Media.File, "{\"src\":\"/media/sample/banner.png\"}");
        _mediaService.Save(image);
    }
}
