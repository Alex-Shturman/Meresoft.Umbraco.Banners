using Meresoft.Umbraco.Banners.Caching;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Caching;

[TestFixture]
public class SnapshotAccessorWarningTests : BannersServiceTestBase
{
    private CapturingLogger _logger = null!;
    private LiveSnapshotAccessor _accessor = null!;

    [SetUp]
    public void CreateAccessor()
    {
        _logger = new CapturingLogger();
        _accessor = ActivatorUtilities.CreateInstance<LiveSnapshotAccessor>(Services, (ILogger<LiveSnapshotAccessor>)_logger);
    }

    [Test]
    public void ReadingTheSnapshotInTheShortWindowBeforeTheStartupBuildHasFinished_IsNotAWarning()
    {
        for (int i = 0; i < 5; i++)
        {
            _ = _accessor.Current;
        }

        Assert.That(_accessor.IsBuilt, Is.False);
        Assert.That(_logger.Levels, Does.Not.Contain(LogLevel.Warning));
        Assert.That(_logger.Levels.Count(l => l == LogLevel.Debug), Is.EqualTo(1), "noted once at Debug");
    }

    [Test]
    public void ReadingTheSnapshotAfterTheStartupBuildFinishedWithoutSuccess_LogsOneWarning_NotOnePerRead()
    {
        _accessor.MarkStartupBuildFinished();

        for (int i = 0; i < 5; i++)
        {
            _ = _accessor.Current;
        }

        Assert.That(_accessor.IsBuilt, Is.False);
        Assert.That(_logger.Levels.Count(l => l == LogLevel.Warning), Is.EqualTo(1));
    }

    [Test]
    public async Task ReadingTheSnapshotAfterTheFirstBuild_LogsNoWarning()
    {
        await _accessor.RebuildAsync();
        _accessor.MarkStartupBuildFinished();

        _ = _accessor.Current;

        Assert.That(_accessor.IsBuilt, Is.True);
        Assert.That(_logger.Levels, Does.Not.Contain(LogLevel.Warning));
    }

    private sealed class CapturingLogger : ILogger<LiveSnapshotAccessor>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Levels.Add(logLevel);
    }
}
