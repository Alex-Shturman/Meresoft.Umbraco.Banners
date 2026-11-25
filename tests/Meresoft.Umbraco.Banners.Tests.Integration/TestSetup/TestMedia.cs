using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;

/// <summary>Creates invented media items for banner tests.</summary>
internal static class TestMedia
{
    /// <summary>Saves an image media item whose file is an invented path and returns it.</summary>
    public static IMedia CreateImage(IMediaService mediaService, string name = "Invented banner image")
    {
        IMedia media = mediaService.CreateMedia(name, Constants.System.Root, Constants.Conventions.MediaTypes.Image);
        media.SetValue(Constants.Conventions.Media.File, "{\"src\":\"/media/invented/banner.png\"}");
        mediaService.Save(media);
        return media;
    }
}
