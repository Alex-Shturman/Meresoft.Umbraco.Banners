using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Extensions;

namespace Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;

/// <summary>Creates invented media items for banner tests.</summary>
internal static class TestMedia
{
    /// <summary>Saves an image media item whose file is an invented path and returns it.</summary>
    public static IMedia CreateImage(IMediaService mediaService, string name = "Invented banner image", int parentId = Constants.System.Root)
    {
        IMedia media = mediaService.CreateMedia(name, parentId, Constants.Conventions.MediaTypes.Image);
        media.SetValue(Constants.Conventions.Media.File, "{\"src\":\"/media/invented/banner.png\"}");
        mediaService.Save(media);
        return media;
    }

    /// <summary>Saves an image media item that has no file, and returns it.</summary>
    public static IMedia CreateImageWithoutFile(IMediaService mediaService)
    {
        IMedia media = mediaService.CreateMedia("Invented image without a file", Constants.System.Root, Constants.Conventions.MediaTypes.Image);
        mediaService.Save(media);
        return media;
    }

    /// <summary>Saves a media folder and returns it.</summary>
    public static IMedia CreateFolder(IMediaService mediaService, string name = "Invented folder")
    {
        IMedia folder = mediaService.CreateMedia(name, Constants.System.Root, Constants.Conventions.MediaTypes.Folder);
        mediaService.Save(folder);
        return folder;
    }
}
