using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Services.Audit;
using Meresoft.Umbraco.Banners.Services.Models;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Services;

/// <inheritdoc cref="IBannerService" />
internal sealed class BannerService : IBannerService
{
    private const int MaxNameLength = 255;
    private const string CopySuffix = " (copy)";

    private readonly IScopeProvider _scopeProvider;
    private readonly IBannerRepository _banners;
    private readonly IZoneRepository _zones;
    private readonly BannerAuditWriter _audit;
    private readonly DistributedCache _distributedCache;
    private readonly IPublishedMediaCache _mediaCache;
    private readonly BannerStateCalculator _stateCalculator;
    private readonly TimeProvider _timeProvider;

    public BannerService(
        IScopeProvider scopeProvider,
        IBannerRepository banners,
        IZoneRepository zones,
        BannerAuditWriter audit,
        DistributedCache distributedCache,
        IPublishedMediaCache mediaCache,
        BannerStateCalculator stateCalculator,
        TimeProvider timeProvider)
    {
        _scopeProvider = scopeProvider;
        _banners = banners;
        _zones = zones;
        _audit = audit;
        _distributedCache = distributedCache;
        _mediaCache = mediaCache;
        _stateCalculator = stateCalculator;
        _timeProvider = timeProvider;
    }

    public async Task<Banner?> GetAsync(Guid key)
    {
        Banner? banner;
        using (_scopeProvider.CreateScope(autoComplete: true))
        {
            banner = _banners.GetByKey(key);
        }

        if (banner is not null)
        {
            await PopulateAsync([banner]);
        }

        return banner;
    }

    public async Task<PagedModel<BannerListItem>> GetPagedAsync(BannerQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        IReadOnlyList<Banner> banners;
        long total;
        Dictionary<int, Guid> zoneKeys;
        using (_scopeProvider.CreateScope(autoComplete: true))
        {
            var filter = new BannerFilter { Type = query.Type, State = query.State, Skip = query.Skip, Take = query.Take };
            if (query.ZoneKey is { } zoneKey)
            {
                BannerZone? zone = _zones.GetByKey(zoneKey);
                if (zone is null)
                {
                    return new PagedModel<BannerListItem>(0, []);
                }

                filter.ZoneId = zone.Id;
            }

            (banners, total) = _banners.GetPaged(filter);
            zoneKeys = _zones.GetAll().ToDictionary(z => z.Id, z => z.Key);
        }

        await PopulateAsync(banners);

        IEnumerable<BannerListItem> items = banners.Select(b =>
        {
            BannerVersion? inUse = b.Live ?? b.Draft;
            return new BannerListItem
            {
                Key = b.Key,
                Name = b.Name,
                Type = b.Type,
                State = b.State,
                Flags = b.Flags,
                ZoneKeys = inUse is null ? [] : inUse.ZoneIds.Where(zoneKeys.ContainsKey).Select(id => zoneKeys[id]).ToList(),
                Weight = inUse?.Weight ?? 1,
                UpdateDate = b.UpdateDate,
            };
        });
        return new PagedModel<BannerListItem>(total, items.ToList());
    }

    public async Task<Attempt<Banner, BannerOperationStatus>> CreateAsync(BannerCreateModel model, Guid userKey)
    {
        ArgumentNullException.ThrowIfNull(model);

        DateTime now = UtcTimestamp.Truncate(_timeProvider.GetUtcNow().UtcDateTime);
        var banner = new Banner
        {
            Key = Guid.NewGuid(),
            Type = model.Type,
            CreateDate = now,
            UpdateDate = now,
            CreatedBy = userKey,
            UpdatedBy = userKey,
        };

        using IScope scope = _scopeProvider.CreateScope();
        FieldValidationResult validation = ApplyDraft(banner, model.Draft, new BannerVersion { Kind = BannerVersionKind.Draft }, now, userKey);
        if (!validation.IsValid)
        {
            return Fail(BannerOperationStatus.InvalidData, banner, validation);
        }

        _banners.Add(banner);
        await _audit.WriteBannerAsync(BannerAuditAction.New, userKey, banner.Id);
        scope.Complete();

        return await SucceedAsync(banner.Key);
    }

    public async Task<Attempt<Banner, BannerOperationStatus>> SaveDraftAsync(Guid key, BannerDraftModel model, Guid userKey)
    {
        ArgumentNullException.ThrowIfNull(model);

        using IScope scope = _scopeProvider.CreateScope();
        Banner? banner = _banners.GetByKey(key);
        if (banner?.Draft is null)
        {
            return Attempt.FailWithStatus<Banner, BannerOperationStatus>(BannerOperationStatus.NotFound, null!);
        }

        if (model.Type is { } type && !BannerVersionValidator.IsTypeChangeAllowed(banner.Type, type))
        {
            return Attempt.FailWithStatus(BannerOperationStatus.TypeChangeNotAllowed, banner);
        }

        if (IsStale(model.UpdateDate, banner))
        {
            return Attempt.FailWithStatus(BannerOperationStatus.ChangedByAnotherUser, banner);
        }

        DateTime now = UtcTimestamp.Truncate(_timeProvider.GetUtcNow().UtcDateTime);
        FieldValidationResult validation = ApplyDraft(banner, model, banner.Draft, now, userKey);
        if (!validation.IsValid)
        {
            return Fail(BannerOperationStatus.InvalidData, banner, validation);
        }

        try
        {
            _banners.SaveDraft(banner);
        }
        catch (InvalidOperationException)
        {
            return Attempt.FailWithStatus(BannerOperationStatus.NotFound, banner);
        }

        await _audit.WriteBannerAsync(BannerAuditAction.Save, userKey, banner.Id);
        scope.Complete();

        return await SucceedAsync(key);
    }

    public async Task<Attempt<Banner, BannerOperationStatus>> PublishAsync(Guid key, Guid userKey, DateTime? expectedUpdateDate = null)
    {
        using IScope scope = _scopeProvider.CreateScope();
        Banner? banner = _banners.GetByKey(key);
        if (banner?.Draft is null)
        {
            return Attempt.FailWithStatus<Banner, BannerOperationStatus>(BannerOperationStatus.NotFound, null!);
        }

        if (IsStale(expectedUpdateDate, banner))
        {
            return Attempt.FailWithStatus(BannerOperationStatus.ChangedByAnotherUser, banner);
        }

        FieldValidationResult validation = BannerVersionValidator.Validate(banner.Type, banner.Draft);
        if (!validation.IsValid)
        {
            return Fail(BannerOperationStatus.InvalidData, banner, validation);
        }

        if (banner.Draft.ZoneIds.Count == 0)
        {
            return Attempt.FailWithStatus(BannerOperationStatus.NoZoneAssigned, banner);
        }

        try
        {
            _banners.CopyDraftOverLive(banner.Id, _timeProvider.GetUtcNow().UtcDateTime);
        }
        catch (InvalidOperationException)
        {
            return Attempt.FailWithStatus(BannerOperationStatus.NotFound, banner);
        }

        await _audit.WriteBannerAsync(BannerAuditAction.Publish, userKey, banner.Id);
        _distributedCache.RefreshBanners(new BannersCacheRefresherPayload { BannerId = banner.Id });
        scope.Complete();

        return await SucceedAsync(key);
    }

    public async Task<Attempt<Banner, BannerOperationStatus>> UnpublishAsync(Guid key, Guid userKey)
    {
        using IScope scope = _scopeProvider.CreateScope();
        Banner? banner = _banners.GetByKey(key);
        if (banner is null)
        {
            return Attempt.FailWithStatus<Banner, BannerOperationStatus>(BannerOperationStatus.NotFound, null!);
        }

        if (banner.IsPublished)
        {
            try
            {
                _banners.SetPublished(banner.Id, false);
            }
            catch (InvalidOperationException)
            {
                return Attempt.FailWithStatus(BannerOperationStatus.NotFound, banner);
            }

            await _audit.WriteBannerAsync(BannerAuditAction.Unpublish, userKey, banner.Id);
            _distributedCache.RefreshBanners(new BannersCacheRefresherPayload { BannerId = banner.Id });
        }

        scope.Complete();
        return await SucceedAsync(key);
    }

    public async Task<Attempt<Banner, BannerOperationStatus>> CopyAsync(Guid key, string? newName, Guid userKey)
    {
        using IScope scope = _scopeProvider.CreateScope();
        Banner? source = _banners.GetByKey(key);
        if (source?.Draft is null)
        {
            return Attempt.FailWithStatus<Banner, BannerOperationStatus>(BannerOperationStatus.NotFound, null!);
        }

        string name = string.IsNullOrWhiteSpace(newName)
            ? (source.Name.Length > MaxNameLength - CopySuffix.Length ? source.Name[..(MaxNameLength - CopySuffix.Length)] : source.Name) + CopySuffix
            : newName.Trim();
        FieldValidationResult nameCheck = BannerVersionValidator.ValidateName(name);
        if (!nameCheck.IsValid)
        {
            return Fail(BannerOperationStatus.InvalidData, source, nameCheck);
        }

        DateTime now = UtcTimestamp.Truncate(_timeProvider.GetUtcNow().UtcDateTime);
        BannerVersion d = source.Draft;
        var copy = new Banner
        {
            Key = Guid.NewGuid(),
            Name = name,
            Type = source.Type,
            CreateDate = now,
            UpdateDate = now,
            CreatedBy = userKey,
            UpdatedBy = userKey,
            Draft = new BannerVersion
            {
                Kind = BannerVersionKind.Draft,
                MediaKey = d.MediaKey,
                Html = d.Html,
                LinkUrl = d.LinkUrl,
                OpenInNewWindow = d.OpenInNewWindow,
                AltText = d.AltText,
                Title = d.Title,
                Weight = d.Weight,
                StartUtc = d.StartUtc,
                EndUtc = d.EndUtc,
                ImpressionLimit = d.ImpressionLimit,
                ClickLimit = d.ClickLimit,
                SavedDate = now,
                SavedBy = userKey,
                ZoneIds = [.. d.ZoneIds],
            },
        };

        _banners.Add(copy);
        await _audit.WriteBannerAsync(BannerAuditAction.Copy, userKey, copy.Id);
        scope.Complete();

        return await SucceedAsync(copy.Key);
    }

    public async Task<BannerOperationStatus> DeleteAsync(Guid key, Guid userKey)
    {
        using IScope scope = _scopeProvider.CreateScope();
        Banner? banner = _banners.GetByKey(key);
        if (banner is null)
        {
            return BannerOperationStatus.NotFound;
        }

        _banners.Delete(banner.Id);
        await _audit.WriteBannerAsync(BannerAuditAction.Delete, userKey, banner.Id);
        _distributedCache.RefreshBanners(new BannersCacheRefresherPayload { BannerId = banner.Id });
        scope.Complete();

        return BannerOperationStatus.Success;
    }

    private static bool IsStale(DateTime? loaded, Banner banner) =>
        loaded is { } value && UtcTimestamp.Truncate(value) != banner.UpdateDate;

    private static Attempt<Banner, BannerOperationStatus> Fail(BannerOperationStatus status, Banner banner, FieldValidationResult validation) =>
        Attempt.FailWithStatus(status, banner, new BannerValidationException(validation));

    /// <summary>Re-reads the stored banner, so the caller sees exactly what was saved (truncated timestamps, ids).</summary>
    private async Task<Attempt<Banner, BannerOperationStatus>> SucceedAsync(Guid key)
    {
        Banner banner;
        using (_scopeProvider.CreateScope(autoComplete: true))
        {
            banner = _banners.GetByKey(key)!;
        }

        await PopulateAsync([banner]);
        return Attempt.SucceedWithStatus(BannerOperationStatus.Success, banner);
    }

    /// <summary>Copies the model into the version and the banner row, mapping zone keys to ids; returns the validation result.</summary>
    private FieldValidationResult ApplyDraft(Banner banner, BannerDraftModel model, BannerVersion draft, DateTime now, Guid userKey)
    {
        banner.Name = model.Name?.Trim() ?? string.Empty;
        banner.UpdateDate = now;
        banner.UpdatedBy = userKey;

        draft.MediaKey = model.MediaKey;
        draft.Html = model.Html;
        draft.LinkUrl = string.IsNullOrWhiteSpace(model.LinkUrl) ? null : model.LinkUrl.Trim();
        draft.OpenInNewWindow = model.OpenInNewWindow;
        draft.AltText = model.AltText?.Trim() ?? string.Empty;
        draft.Title = string.IsNullOrWhiteSpace(model.Title) ? null : model.Title.Trim();
        draft.Weight = model.Weight;
        draft.StartUtc = UtcTimestamp.Truncate(model.StartUtc);
        draft.EndUtc = UtcTimestamp.Truncate(model.EndUtc);
        draft.ImpressionLimit = model.ImpressionLimit;
        draft.ClickLimit = model.ClickLimit;
        draft.SavedDate = now;
        draft.SavedBy = userKey;
        banner.Draft = draft;

        FieldValidationResult result = BannerVersionValidator.ValidateName(banner.Name);
        FieldValidationResult versionResult = BannerVersionValidator.Validate(banner.Type, draft);
        foreach ((string field, string message) in versionResult.Errors)
        {
            result.Add(field, message);
        }

        var zoneIds = new List<int>();
        foreach (Guid zoneKey in model.ZoneKeys.Distinct())
        {
            BannerZone? zone = _zones.GetByKey(zoneKey);
            if (zone is null)
            {
                result.Add("zoneKeys", "A selected zone does not exist.");
                continue;
            }

            zoneIds.Add(zone.Id);
        }

        draft.ZoneIds = zoneIds;
        return result;
    }

    /// <summary>Sets <see cref="Banner.State"/> and <see cref="Banner.Flags"/>.</summary>
    private async Task PopulateAsync(IReadOnlyList<Banner> banners)
    {
        var mediaExists = new Dictionary<Guid, bool>();
        foreach (Banner banner in banners)
        {
            BannerVersion? inUse = banner.Live ?? banner.Draft;
            bool exists = true;
            if (banner.Type == BannerType.Image && inUse?.MediaKey is { } mediaKey)
            {
                if (!mediaExists.TryGetValue(mediaKey, out exists))
                {
                    exists = await _mediaCache.GetByIdAsync(mediaKey) is not null;
                    mediaExists[mediaKey] = exists;
                }
            }
            else if (banner.Type == BannerType.Image)
            {
                exists = false;
            }

            banner.State = _stateCalculator.GetState(banner);

            // Totals for the limit flag are added with the statistics (limits story); zero until then.
            banner.Flags = _stateCalculator.GetFlags(banner, new BannerRuntimeInfo(exists, 0, 0));
        }
    }
}
