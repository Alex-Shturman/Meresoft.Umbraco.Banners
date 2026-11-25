namespace Meresoft.Umbraco.Banners.Domain;

/// <summary>A banner with its draft and live versions.</summary>
public class Banner
{
    /// <summary>Internal id used in statistics, click addresses and view tokens.</summary>
    public int Id { get; set; }

    /// <summary>Id used by the management API.</summary>
    public Guid Key { get; set; }

    /// <summary>Name, shown in the back office only.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Image or html; fixed after creation.</summary>
    public BannerType Type { get; set; }

    /// <summary>True while the live version is shown on the site.</summary>
    public bool IsPublished { get; set; }

    /// <summary>Creation time, UTC.</summary>
    public DateTime CreateDate { get; set; }

    /// <summary>Last change time, UTC.</summary>
    public DateTime UpdateDate { get; set; }

    /// <summary>Key of the creating user.</summary>
    public Guid CreatedBy { get; set; }

    /// <summary>Key of the last editing user.</summary>
    public Guid UpdatedBy { get; set; }

    /// <summary>Time of the last publish, UTC.</summary>
    public DateTime? PublishDate { get; set; }

    /// <summary>The draft version.</summary>
    public BannerVersion? Draft { get; set; }

    /// <summary>The live version; null if never published.</summary>
    public BannerVersion? Live { get; set; }
}
