using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;

namespace Meresoft.Umbraco.Banners.ManagementApi.History;

/// <summary>
/// Reads the audit entries of one banner or zone. Umbraco's audit service filters by entity id only, and banners,
/// zones and content share the id space, so the entity type is filtered here and the paging is done after it.
/// </summary>
public sealed class BannerHistoryReader
{
    private const int FetchSize = 200;
    private const string UnknownUser = "(unknown user)";

    private readonly IAuditService _audit;
    private readonly IUserService _users;

    /// <summary>Creates the reader.</summary>
    public BannerHistoryReader(IAuditService audit, IUserService users)
    {
        _audit = audit ?? throw new ArgumentNullException(nameof(audit));
        _users = users ?? throw new ArgumentNullException(nameof(users));
    }

    /// <summary>A page of the history of the entity, newest first.</summary>
    /// <param name="entityId">Internal id of the banner or zone.</param>
    /// <param name="entityType">The audit entity type of the package.</param>
    /// <param name="skip">Entries to skip.</param>
    /// <param name="take">Entries to return.</param>
    public async Task<HistoryPageModel> ReadAsync(int entityId, string entityType, int skip, int take)
    {
        var matching = new List<IAuditItem>();
        for (int fetched = 0; ; fetched += FetchSize)
        {
            IAuditItem[] chunk = (await _audit.GetItemsByEntityAsync(entityId, fetched, FetchSize)).Items.ToArray();
            matching.AddRange(chunk.Where(item => item.EntityType == entityType));
            if (chunk.Length < FetchSize)
            {
                break;
            }
        }

        IAuditItem[] page = matching.Skip(skip).Take(take).ToArray();
        Dictionary<int, string> names = _users.GetUsersById(page.Select(p => p.UserId).Distinct().ToArray())
            .ToDictionary(u => u.Id, u => u.Name ?? UnknownUser);

        return new HistoryPageModel
        {
            Total = matching.Count,
            Items = page.Select(item => new HistoryEntryModel
            {
                UserName = names.GetValueOrDefault(item.UserId, UnknownUser),
                Action = item.AuditType.ToString().ToLowerInvariant(),
                Date = DateTime.SpecifyKind(item.CreateDate, DateTimeKind.Utc),
            }).ToList(),
        };
    }
}
