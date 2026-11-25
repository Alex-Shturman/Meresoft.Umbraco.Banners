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
    public void ReadingTheSnapshotBeforeTheFirstBuild_LogsOneWarning_NotOnePerRead()
    {
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
