using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace Meresoft.Umbraco.Banners.Counting;

/// <summary>Writes the counts still in memory when the application stops, so a clean shutdown loses nothing.</summary>
internal sealed class FlushOnStoppingHandler : INotificationHandler<UmbracoApplicationStoppingNotification>
{
    private readonly CounterFlusher _flusher;

    public FlushOnStoppingHandler(CounterFlusher flusher) => _flusher = flusher;

    public void Handle(UmbracoApplicationStoppingNotification notification) => _flusher.Flush();
}
