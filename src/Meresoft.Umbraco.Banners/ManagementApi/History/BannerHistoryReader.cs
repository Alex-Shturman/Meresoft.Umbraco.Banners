using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.ManagementApi.History;

/// <summary>
/// Reads the audit history of one banner or zone: the database filters by entity id and entity type and pages, so the
/// total counts only the entries of this entity type, and all user names are looked up in one batch.
/// </summary>
public sealed class BannerHistoryReader
{
    /// <summary>The most entries that can be skipped; deeper pages are refused by the controllers.</summary>
    public const int MaxSkip = 10_000;

    private readonly IAuditHistoryRepository _history;
    private readonly IScopeProvider _scopeProvider;
    private readonly IUserService _users;
    private readonly IUserIdKeyResolver _userIdKeyResolver;

    // Internal: the repository type is internal. The reader is registered with a factory (see AddMeresoftBanners).
    internal BannerHistoryReader(
        IAuditHistoryRepository history,
        IScopeProvider scopeProvider,
        IUserService users,
        IUserIdKeyResolver userIdKeyResolver)
    {
        _history = history ?? throw new ArgumentNullException(nameof(history));
        _scopeProvider = scopeProvider ?? throw new ArgumentNullException(nameof(scopeProvider));
        _users = users ?? throw new ArgumentNullException(nameof(users));
        _userIdKeyResolver = userIdKeyResolver ?? throw new ArgumentNullException(nameof(userIdKeyResolver));
    }

    /// <summary>A page of the history of the entity, newest first.</summary>
    /// <param name="entityId">Internal id of the banner or zone.</param>
    /// <param name="entityType">The audit entity type of the package.</param>
    /// <param name="skip">Entries to skip, at most <see cref="MaxSkip"/>.</param>
    /// <param name="take">Entries to return.</param>
    public async Task<HistoryPageModel> ReadAsync(int entityId, string entityType, int skip, int take)
    {
        // The scope ends before the user lookups below, so it is not held across those awaits.
        (long total, IReadOnlyList<AuditHistoryRow> rows) result;
        using (_scopeProvider.CreateScope(autoComplete: true))
        {
            result = _history.GetPage(entityId, entityType, skip, take);
        }

        Dictionary<int, string> names = await ResolveNamesAsync(result.rows.Select(r => r.UserId).Distinct().ToArray());
        return new HistoryPageModel
        {
            Total = result.total,
            Items = result.rows.Select(row => new HistoryEntryModel
            {
                UserName = names.GetValueOrDefault(row.UserId),
                Action = row.Action.ToLowerInvariant(),
                Date = row.CreatedUtc,
            }).ToList(),
        };
    }

    /// <summary>Turns user ids into names with one lookup of all the keys; ids of deleted users are left out.</summary>
    private async Task<Dictionary<int, string>> ResolveNamesAsync(int[] userIds)
    {
        var keyById = new Dictionary<int, Guid>();
        foreach (int id in userIds)
        {
            Attempt<Guid> key = await _userIdKeyResolver.TryGetAsync(id);
            if (key.Success)
            {
                keyById[id] = key.Result;
            }
        }

        Dictionary<Guid, string> nameByKey = (await _users.GetAsync(keyById.Values))
            .ToDictionary(user => user.Key, user => user.Name ?? string.Empty);
        return keyById
            .Where(pair => nameByKey.ContainsKey(pair.Value))
            .ToDictionary(pair => pair.Key, pair => nameByKey[pair.Value]);
    }
}
