namespace Meresoft.Umbraco.Banners.ManagementApi.History;

/// <summary>One change of a banner or zone, from the audit log (FR-018).</summary>
public class HistoryEntryModel
{
    /// <summary>Name of the back office user who made the change; null when the user no longer exists (the back office shows its own text).</summary>
    public string? UserName { get; set; }

    /// <summary>The action: new, save, publish, unpublish, copy or delete.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>When it happened, UTC.</summary>
    public DateTime Date { get; set; }
}

/// <summary>A page of history entries, newest first.</summary>
public class HistoryPageModel
{
    /// <summary>All entries of the item, not only this page.</summary>
    public long Total { get; set; }

    /// <summary>The entries of this page.</summary>
    public IReadOnlyList<HistoryEntryModel> Items { get; set; } = [];
}
