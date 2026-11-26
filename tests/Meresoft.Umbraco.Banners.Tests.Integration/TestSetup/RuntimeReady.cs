using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;

namespace Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;

/// <summary>
/// Umbraco 17 runs the unattended upgrade, which includes this package's migration plan, in a background service
/// (UnattendedUpgradeBackgroundService) after the test host has started. Until it is done the runtime level is below
/// <see cref="RuntimeLevel.Run"/> and every request is answered with a 503. SQLite finishes before the first request;
/// SQL Server does not, so a test that sends HTTP requests waits first.
/// Reaching Run is not enough: MaintenanceModeActionFilter also answers 503 ("starting up") while
/// <see cref="IContentRoutingReadiness.IsReady"/> is still false at the Run level (the initialization window), so both are awaited.
/// </summary>
public static class RuntimeReady
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(120);

    public static async Task WaitForRunLevelAsync(IRuntimeState runtime, IContentRoutingReadiness readiness)
    {
        DateTime stop = DateTime.UtcNow + Timeout;
        while (!(runtime.Level == RuntimeLevel.Run && readiness.IsReady) && runtime.Level != RuntimeLevel.BootFailed && DateTime.UtcNow < stop)
        {
            await Task.Delay(50);
        }

        Assert.That(runtime.Level == RuntimeLevel.Run && readiness.IsReady, Is.True,
            $"the application was not ready within {Timeout.TotalSeconds:0} seconds (level: {runtime.Level}, reason: {runtime.Reason}, content routing ready: {readiness.IsReady}, error: {runtime.BootFailedException?.Message})");
    }
}
