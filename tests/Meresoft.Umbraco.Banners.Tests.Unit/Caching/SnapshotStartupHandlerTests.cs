using Meresoft.Umbraco.Banners.Caching;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.Services;

namespace Meresoft.Umbraco.Banners.Tests.Unit.Caching;

[TestFixture]
public class SnapshotStartupHandlerTests
{
    private ILiveSnapshotAccessor _accessor = null!;
    private IRuntimeState _runtime = null!;
    private CapturingLogger _logger = null!;
    private FakeTimeProvider _time = null!;
    private SnapshotStartupHandler _handler = null!;

    [SetUp]
    public void SetUp()
    {
        _accessor = Substitute.For<ILiveSnapshotAccessor>();
        _runtime = Substitute.For<IRuntimeState>();
        _runtime.Level.Returns(RuntimeLevel.Run);
        _logger = new CapturingLogger();
        _time = new FakeTimeProvider();
        _handler = new SnapshotStartupHandler(_accessor, _runtime, _time, _logger);
    }

    private Task Run(CancellationToken token = default) =>
        _handler.HandleAsync(new UmbracoApplicationStartedNotification(false), token);

    /// <summary>Lets the handler run through its backoff delays by moving the fake clock.</summary>
    private async Task CompleteWithBackoff(Task handler)
    {
        for (int i = 0; i < 100 && !handler.IsCompleted; i++)
        {
            _time.Advance(TimeSpan.FromSeconds(5));
            await Task.Yield();
            await Task.Delay(5);
        }

        await handler;
    }

    [Test]
    public async Task WhenTheFirstBuildWorks_BuildsOnce()
    {
        await Run();

        await _accessor.Received(1).RebuildAsync(Arg.Any<IReadOnlySet<Guid>?>());
        _accessor.Received(1).MarkStartupBuildFinished();
        Assert.That(_logger.Levels, Is.Empty);
    }

    [Test]
    public async Task WhenTheRuntimeIsNotRunning_BuildsNothing()
    {
        _runtime.Level.Returns(RuntimeLevel.Install);

        await Run();

        await _accessor.DidNotReceive().RebuildAsync(Arg.Any<IReadOnlySet<Guid>?>());
        _accessor.DidNotReceive().MarkStartupBuildFinished();
    }

    [Test]
    public async Task WhenTheBuildFailsTwice_ItRetriesWithBackoff_AndSucceeds()
    {
        int calls = 0;
        _accessor.RebuildAsync(Arg.Any<IReadOnlySet<Guid>?>()).Returns(_ => ++calls < 3 ? Task.FromException(new InvalidOperationException("tables missing")) : Task.CompletedTask);

        await CompleteWithBackoff(Run());

        Assert.That(calls, Is.EqualTo(3));
    }

    [Test]
    public async Task WhenTheBuildKeepsFailing_ItGivesUpAfterAFewTries_LogsAnError_AndDoesNotStopStartup()
    {
        _accessor.RebuildAsync(Arg.Any<IReadOnlySet<Guid>?>()).Returns(Task.FromException(new InvalidOperationException("tables missing")));

        await CompleteWithBackoff(Run());

        await _accessor.Received(4).RebuildAsync(Arg.Any<IReadOnlySet<Guid>?>());
        Assert.That(_logger.Levels, Does.Contain(LogLevel.Error));
        _accessor.Received(1).MarkStartupBuildFinished();
    }

    [Test]
    public void WhenStartupIsCancelled_TheCancellationIsNotSwallowed()
    {
        using var source = new CancellationTokenSource();
        _accessor.RebuildAsync(Arg.Any<IReadOnlySet<Guid>?>()).Returns(Task.FromException(new OperationCanceledException(source.Token)));

        Assert.That(async () => await Run(source.Token), Throws.InstanceOf<OperationCanceledException>());
        _accessor.DidNotReceive().MarkStartupBuildFinished();
    }

    private sealed class CapturingLogger : ILogger<SnapshotStartupHandler>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
    }
}
