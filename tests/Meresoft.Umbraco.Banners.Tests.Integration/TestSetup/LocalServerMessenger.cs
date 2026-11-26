using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Sync;

namespace Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;

/// <summary>
/// Delivers payload refresh messages to this server only, and immediately, without looking at scopes. The test host
/// default is a messenger that delivers nothing, so cache refreshers would never run. Delivering immediately is the
/// strictest case for the package: it must defer its own messages until the scope has committed. Other message kinds are ignored.
/// </summary>
public sealed class LocalServerMessenger : IServerMessenger
{
    private readonly IJsonSerializer _serializer;

    public LocalServerMessenger(IJsonSerializer serializer) => _serializer = serializer;

    public void QueueRefresh<TPayload>(ICacheRefresher refresher, TPayload[] payload)
    {
        if (refresher is IJsonCacheRefresher json)
        {
            json.Refresh(_serializer.Serialize(payload));
        }
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
