using System.Data.Common;
using System.Runtime.ExceptionServices;
using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Services.Audit;
using Meresoft.Umbraco.Banners.Services.Models;
using Umbraco.Cms.Core;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Services;

/// <inheritdoc cref="IBannerZoneService" />
internal sealed class BannerZoneService : IBannerZoneService
{
    private readonly IScopeProvider _scopeProvider;
    private readonly IZoneRepository _zones;
    private readonly IRequestedZoneRepository _requestedZones;
    private readonly BannerAuditWriter _audit;
    private readonly TimeProvider _timeProvider;

    public BannerZoneService(
        IScopeProvider scopeProvider,
        IZoneRepository zones,
        IRequestedZoneRepository requestedZones,
        BannerAuditWriter audit,
        TimeProvider timeProvider)
    {
        _scopeProvider = scopeProvider;
        _zones = zones;
        _requestedZones = requestedZones;
        _audit = audit;
        _timeProvider = timeProvider;
    }

    public Task<IReadOnlyList<BannerZone>> GetAllAsync()
    {
        using IScope scope = _scopeProvider.CreateScope(autoComplete: true);
        return Task.FromResult(_zones.GetAll());
    }

    public Task<BannerZone?> GetAsync(Guid key)
    {
        using IScope scope = _scopeProvider.CreateScope(autoComplete: true);
        return Task.FromResult(_zones.GetByKey(key));
    }

    public Task<BannerZone?> GetByAliasAsync(string alias)
    {
        using IScope scope = _scopeProvider.CreateScope(autoComplete: true);
        return Task.FromResult(_zones.GetByAlias(AliasNormalizer.Normalize(alias)));
    }

    public Task<IReadOnlyList<Guid>> GetAssignedBannerKeysAsync(Guid zoneKey)
    {
        using IScope scope = _scopeProvider.CreateScope(autoComplete: true);
        BannerZone? zone = _zones.GetByKey(zoneKey);
        return Task.FromResult(zone is null ? [] : _zones.GetAssignedBannerKeys(zone.Id));
    }

    public Task<IReadOnlyList<RequestedZone>> GetRequestedZonesAsync()
    {
        using IScope scope = _scopeProvider.CreateScope(autoComplete: true);
        return Task.FromResult(_requestedZones.GetAll());
    }

    public Task DismissRequestedZoneAsync(string alias)
    {
        using IScope scope = _scopeProvider.CreateScope();
        scope.EagerWriteLock(BannersConstants.WriteLockId);
        _requestedZones.Delete(AliasNormalizer.Normalize(alias));
        scope.Complete();
        return Task.CompletedTask;
    }

    public async Task<Attempt<BannerZone, BannerOperationStatus>> CreateAsync(BannerZoneModel model, Guid userKey)
    {
        ArgumentNullException.ThrowIfNull(model);

        DateTime now = UtcTimestamp.Truncate(_timeProvider.GetUtcNow().UtcDateTime);
        var zone = new BannerZone { Key = Guid.NewGuid(), CreateDate = now, UpdateDate = now };
        Apply(model, zone);

        FieldValidationResult validation = ZoneValidator.Validate(zone);
        if (!validation.IsValid)
        {
            return Attempt.FailWithStatus<BannerZone, BannerOperationStatus>(BannerOperationStatus.InvalidData, zone, new BannerValidationException(validation));
        }

        DbException? failure = null;
        using (IScope scope = _scopeProvider.CreateScope())
        {
            scope.EagerWriteLock(BannersConstants.WriteLockId);
            if (_zones.GetByAlias(zone.Alias) is not null)
            {
                return Attempt.FailWithStatus(BannerOperationStatus.DuplicateAlias, zone);
            }

            try
            {
                _zones.Add(zone);
                _requestedZones.Delete(zone.Alias);
                await _audit.WriteZoneAsync(BannerAuditAction.New, userKey, zone.Id);
                scope.Notifications.Publish(new BannersChangedNotification(new BannersCacheRefresherPayload { ZoneId = zone.Id }));
                scope.Complete();
                return Attempt.SucceedWithStatus(BannerOperationStatus.Success, zone);
            }
            catch (DbException ex)
            {
                failure = ex; // The scope ends here without completing, which rolls the attempt back.
            }
        }

        return await DuplicateOrRethrowAsync(zone.Alias, zone, failure);
    }

    public async Task<Attempt<BannerZone, BannerOperationStatus>> UpdateAsync(Guid key, BannerZoneModel model, Guid userKey)
    {
        ArgumentNullException.ThrowIfNull(model);

        DbException? failure = null;
        using (IScope scope = _scopeProvider.CreateScope())
        {
            scope.EagerWriteLock(BannersConstants.WriteLockId);
            try
            {
                return await UpdateInScopeAsync(scope, key, model, userKey);
            }
            catch (DbException ex)
            {
                failure = ex; // The scope ends here without completing, which rolls the attempt back.
            }
        }

        return await DuplicateOrRethrowAsync(AliasNormalizer.Normalize(model.Alias), null, failure);
    }

    private async Task<Attempt<BannerZone, BannerOperationStatus>> UpdateInScopeAsync(IScope scope, Guid key, BannerZoneModel model, Guid userKey)
    {
        BannerZone? zone = _zones.GetByKey(key);
        if (zone is null)
        {
            return Attempt.FailWithStatus<BannerZone, BannerOperationStatus>(BannerOperationStatus.NotFound, null!);
        }

        if (model.UpdateDate is { } loaded && UtcTimestamp.Truncate(loaded) != zone.UpdateDate)
        {
            return Attempt.FailWithStatus(BannerOperationStatus.ChangedByAnotherUser, zone);
        }

        DateTime loadedUpdateDate = zone.UpdateDate;
        Apply(model, zone);
        zone.UpdateDate = UtcTimestamp.Truncate(_timeProvider.GetUtcNow().UtcDateTime);

        FieldValidationResult validation = ZoneValidator.Validate(zone);
        if (!validation.IsValid)
        {
            return Attempt.FailWithStatus(BannerOperationStatus.InvalidData, zone, new BannerValidationException(validation));
        }

        BannerZone? sameAlias = _zones.GetByAlias(zone.Alias);
        if (sameAlias is not null && sameAlias.Id != zone.Id)
        {
            return Attempt.FailWithStatus(BannerOperationStatus.DuplicateAlias, zone);
        }

        // Guarded by the update date read above, so a save by someone else in between is not overwritten.
        if (!_zones.Update(zone, loadedUpdateDate))
        {
            return Attempt.FailWithStatus(BannerOperationStatus.ChangedByAnotherUser, zone);
        }

        _requestedZones.Delete(zone.Alias);
        await _audit.WriteZoneAsync(BannerAuditAction.Save, userKey, zone.Id);
        scope.Notifications.Publish(new BannersChangedNotification(new BannersCacheRefresherPayload { ZoneId = zone.Id }));
        scope.Complete();

        return Attempt.SucceedWithStatus(BannerOperationStatus.Success, zone);
    }

    /// <summary>
    /// After a database error: when the alias exists now, another editor won the race and the answer is DuplicateAlias
    /// (checked in a fresh scope, because the failed one is rolled back); otherwise the error is not about the alias and is rethrown.
    /// </summary>
    private Task<Attempt<BannerZone, BannerOperationStatus>> DuplicateOrRethrowAsync(string alias, BannerZone? zone, DbException failure)
    {
        using (_scopeProvider.CreateScope(autoComplete: true))
        {
            if (_zones.GetByAlias(alias) is not null)
            {
                return Task.FromResult(Attempt.FailWithStatus(BannerOperationStatus.DuplicateAlias, zone!));
            }
        }

        ExceptionDispatchInfo.Capture(failure).Throw();
        throw failure; // Not reached; keeps the compiler satisfied.
    }

    public async Task<BannerOperationStatus> DeleteAsync(Guid key, Guid userKey)
    {
        using IScope scope = _scopeProvider.CreateScope();
        scope.EagerWriteLock(BannersConstants.WriteLockId);
        BannerZone? zone = _zones.GetByKey(key);
        if (zone is null)
        {
            return BannerOperationStatus.NotFound;
        }

        if (_zones.GetAssignedBannerKeys(zone.Id).Count > 0)
        {
            return BannerOperationStatus.ZoneInUse;
        }

        _zones.Delete(zone.Id);
        await _audit.WriteZoneAsync(BannerAuditAction.Delete, userKey, zone.Id);
        scope.Notifications.Publish(new BannersChangedNotification(new BannersCacheRefresherPayload { ZoneId = zone.Id }));
        scope.Complete();

        return BannerOperationStatus.Success;
    }

    private static void Apply(BannerZoneModel model, BannerZone zone)
    {
        zone.Alias = AliasNormalizer.Normalize(model.Alias);
        zone.Name = model.Name?.Trim() ?? string.Empty;
        zone.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description;
        zone.ShowCount = model.ShowCount;
        zone.ImageWidth = model.ImageWidth;
        zone.ImageHeight = model.ImageHeight;
        zone.RenderMode = model.RenderMode;
    }
}
