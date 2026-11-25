using System.Net;
using System.Text.RegularExpressions;
using Meresoft.Umbraco.Banners.Counting;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NUnit.Framework;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Privacy;

/// <summary>FR-014 and constitution III: no IP address, user agent, cookie or visitor id is stored, logged or set.</summary>
[TestFixture]
public class PrivacyTests : PublicEndpointTestBase
{
    private const string DistinctiveAgent = "InventedBrowser/9.9 (Probe; unique-agent-marker-4711) Chrome/131.0.0.0 Safari/537.36";
    private const string VisitorAddress = "203.0.113.77";

    private static readonly CapturingLoggerProvider Sink = new();

    [SetUp]
    public void ClearSink() => Sink.Clear();

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, string? body = null)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.UserAgent.Clear();
        request.Headers.TryAddWithoutValidation("User-Agent", DistinctiveAgent);
        request.Headers.TryAddWithoutValidation("X-Forwarded-For", VisitorAddress);
        request.Headers.TryAddWithoutValidation("Cookie", "visitor=abc123");
        if (body is not null)
        {
            request.Content = PlainText(body);
        }

        return await Anonymous.SendAsync(request);
    }

    [Test]
    public async Task NoPublicEndpointAndNoRenderedZone_SetsACookie()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");
        string token = IssueToken(banner.ZoneId, banner.BannerId);

        HttpResponseMessage click = await SendAsync(HttpMethod.Get, $"/_banners/c/{banner.BannerId}?z={banner.ZoneId}");
        HttpResponseMessage unknown = await SendAsync(HttpMethod.Get, "/_banners/c/99999?z=1");
        HttpResponseMessage seen = await SendAsync(HttpMethod.Post, "/_banners/seen", $"{token} {banner.BannerId}\ngarbage line");
        HttpResponseMessage tooBig = await SendAsync(HttpMethod.Post, "/_banners/seen", new string('x', 20_000));

        foreach (HttpResponseMessage response in new[] { click, unknown, seen, tooBig })
        {
            Assert.That(response.Headers.Contains("Set-Cookie"), Is.False);
        }

        var renderer = GetRequiredService<Meresoft.Umbraco.Banners.Rendering.BannerZoneRenderer>();
        string html = renderer.Render(new Dictionary<object, object?>(), renderer.Select("home-top"), null, null);
        Assert.That(html, Does.Not.Contain("cookie").IgnoreCase);
    }

    [Test]
    public async Task ALogLineOfAFailedFlush_HoldsNoIpAddressUserAgentOrCookie_OnlyIdsAndCounts()
    {
        PublishedBanner banner = await PublishBannerAsync("home-top");
        HttpResponseMessage click = await SendAsync(HttpMethod.Get, $"/_banners/c/{banner.BannerId}?z={banner.ZoneId}");
        Assert.That(click.StatusCode, Is.EqualTo(HttpStatusCode.Redirect));
        // The test host's own logger factory does not take extra providers (it showed no line at all), so the flusher gets
        // a logger from a factory that feeds the capturing sink.
        using ILoggerFactory loggers = LoggerFactory.Create(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(Sink));
        var flusher = new CounterFlusher(Buffer, GetRequiredService<IScopeProvider>(), new FailingStatisticsRepository(), loggers.CreateLogger<CounterFlusher>());

        flusher.Flush();

        IReadOnlyList<string> lines = Sink.Lines("Meresoft.Umbraco.Banners");
        Assert.That(lines, Is.Not.Empty);
        Assert.That(lines.Any(l => l.Contains("failed", StringComparison.OrdinalIgnoreCase)), Is.True, "the failure was logged, so the lines below were really checked.");
        Assert.That(lines, Has.None.Contain(VisitorAddress));
        Assert.That(lines, Has.None.Contain("unique-agent-marker-4711"));
        Assert.That(lines, Has.None.Contain("abc123"));
        Assert.That(lines, Has.None.Contain("visitor="));
        Assert.That(lines.Where(l => Regex.IsMatch(l, @"\b\d{1,3}(\.\d{1,3}){3}\b")), Is.Empty, "no IPv4 address in any package log line");
        Assert.That(Buffer.Swap().Sum(c => c.Clicks), Is.EqualTo(1), "the counts were kept for the next run");
    }

    /// <summary>
    /// Reflection over the schema the migration creates and over the runtime table mappings: no column for an IP address,
    /// a user agent, a cookie, a visitor or a person. Works the same on every database (the SQL Server job of CI runs it too).
    /// </summary>
    [Test]
    public void NoPackageTableHasAColumnForAnIpAddressAUserAgentOrAVisitor()
    {
        string[] forbidden = ["ip", "ipaddress", "useragent", "agent", "visitor", "visitorid", "cookie", "session", "member", "memberid", "email"];
        System.Reflection.Assembly package = typeof(BannersConstants).Assembly;
        Type[] tableTypes = package.GetTypes()
            .Where(t => t.Namespace is { } ns
                && (ns.EndsWith(".Persistence.Migrations.Schema.V1", StringComparison.Ordinal) || ns.EndsWith(".Persistence.Dtos", StringComparison.Ordinal))
                && t.GetCustomAttributes(typeof(NPoco.TableNameAttribute), inherit: false).Length > 0)
            .ToArray();

        var columns = tableTypes
            .SelectMany(t => t.GetProperties().Select(p => (Table: t.Name, Column: p.GetCustomAttributes(typeof(NPoco.ColumnAttribute), false).Cast<NPoco.ColumnAttribute>().FirstOrDefault()?.Name ?? p.Name)))
            .ToList();

        Assert.That(tableTypes, Has.Length.GreaterThanOrEqualTo(12), "six schema types and six runtime types were found");
        Assert.That(columns.Count, Is.GreaterThan(40), "the columns were read");
        Assert.That(columns.Where(c => forbidden.Contains(c.Column.ToLowerInvariant())), Is.Empty);
    }

    private sealed class FailingWriteException : System.Data.Common.DbException
    {
        public FailingWriteException() : base("simulated write failure")
        {
        }
    }

    private sealed class FailingStatisticsRepository : IStatisticsRepository
    {
        public void AddDelta(CounterEntry delta) => throw new FailingWriteException();

        public IReadOnlyList<DailyStatRow> GetDailyRows(DateOnly from, DateOnly to, int? zoneId, int? bannerId) => [];

        public IReadOnlyDictionary<int, Meresoft.Umbraco.Banners.Domain.BannerTotals> GetTotals(IReadOnlyCollection<int> bannerIds) =>
            new Dictionary<int, Meresoft.Umbraco.Banners.Domain.BannerTotals>();
    }

    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<(string Category, string Line)> _lines = [];

        public void Clear()
        {
            lock (_lines)
            {
                _lines.Clear();
            }
        }

        public IReadOnlyList<string> Lines(string categoryPrefix)
        {
            lock (_lines)
            {
                return _lines.Where(l => l.Category.StartsWith(categoryPrefix, StringComparison.Ordinal)).Select(l => l.Line).ToList();
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, this);

        public void Dispose()
        {
        }

        private sealed class CapturingLogger : ILogger
        {
            private readonly string _category;
            private readonly CapturingLoggerProvider _owner;

            public CapturingLogger(string category, CapturingLoggerProvider owner)
            {
                _category = category;
                _owner = owner;
            }

            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                string line = formatter(state, exception) + (exception is null ? string.Empty : " " + exception);
                lock (_owner._lines)
                {
                    _owner._lines.Add((_category, line));
                }
            }
        }
    }
}
