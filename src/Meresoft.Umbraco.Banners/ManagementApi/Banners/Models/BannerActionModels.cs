namespace Meresoft.Umbraco.Banners.ManagementApi.Banners.Models;

/// <summary>Body of the publish request.</summary>
public class PublishBannerRequestModel
{
    /// <summary>The update date loaded by the client; a different stored value gives 409.</summary>
    public DateTime? UpdateDate { get; set; }
}

/// <summary>Body of the copy request.</summary>
public class CopyBannerRequestModel
{
    /// <summary>Name of the copy; empty adds a copy marker to the original name.</summary>
    public string? Name { get; set; }
}
