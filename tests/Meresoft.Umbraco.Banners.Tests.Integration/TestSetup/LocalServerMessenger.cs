using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Scoping;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Sync;

namespace Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;

/// <summary>
/// Delivers payload refresh messages to this server only, after the surrounding scope completes,
/// like Umbraco's database messenger does on a single server. The test host default is a messenger
/// that delivers nothing, so cache refreshers would never run. Other message kinds are ignored.
/// </summary>
public sealed class LocalServerMessenger : IServerMessenger
{
    private readonly ICoreScopeProvider _scopeProvider;
    private readonly IJsonSerializer _serializer;

    public LocalServerMessenger(ICoreScopeProvider scopeProvider, IJsonSerializer serializer)
    {
        _scopeProvider = scopeProvider;
        _serializer = serializer;
    }

    public void QueueRefresh<TPayload>(ICacheRefresher refresher, TPayload[] payload)
    {
        void Deliver()
        {
            if (refresher is IJsonCacheRefresher json)
            {
                json.Refresh(_serializer.Serialize(payload));
            }
        }

        IScopeContext? context = _scopeProvider.Context;
        if (context is null)
        {
            Deliver();
            return;
        }

        context.Enlist(Guid.NewGuid().ToString(), () => 0, (completed, _) =>
        {
            if (completed)
            {
                Deliver();
            }
        });
    }

    public void Sync() { }

    public void SendMessages() { }

    public void QueueRefresh<T>(ICacheRefresher refresher, Func<T, int> getNumericId, params T[] instances) { }

    public void QueueRefresh<T>(ICacheRefresher refresher, Func<T, Guid> getGuidId, params T[] instances) { }

    public void QueueRemove<T>(ICacheRefresher refresher, Func<T, int> getNumericId, params T[] instances) { }

    public void QueueRemove(ICacheRefresher refresher, params int[] numericIds) { }

    public void QueueRefresh(ICacheRefresher refresher, params int[] numericIds) { }

    public void QueueRefresh(ICacheRefresher refresher, params Guid[] guidIds) { }

    public void QueueRefreshAll(ICacheRefresher refresher) { }
}
