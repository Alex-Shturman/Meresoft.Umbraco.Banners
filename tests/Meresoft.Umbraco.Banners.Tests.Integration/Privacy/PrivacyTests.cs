using System.Net;
using System.Text.RegularExpressions;
using Meresoft.Umbraco.Banners.Persistence.Dtos;
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

    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        builder.Services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace).AddProvider(Sink));
    }

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
    public async Task NoLogLineOfThePackage_HoldsAnIpAddressOrAUserAgent_AndNoPublicEndpointSetsACookie()
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

        IReadOnlyList<string> packageLines = Sink.Lines("Meresoft.Umbraco.Banners");
        Assert.That(packageLines, Has.None.Contain(VisitorAddress));
        Assert.That(packageLines, Has.None.Contain("unique-agent-marker-4711"));
        Assert.That(packageLines, Has.None.Contain("abc123"));
        Assert.That(packageLines.Where(l => Regex.IsMatch(l, @"\b\d{1,3}(\.\d{1,3}){3}\b")), Is.Empty, "no IPv4 address in any package log line");
    }

    [Test]
    public void NoPackageTableHasAColumnForAnIpAddressAUserAgentOrAVisitor()
    {
        string[] forbidden = ["ip", "ipaddress", "useragent", "agent", "visitor", "visitorid", "cookie", "session", "member", "memberid", "email"];
        using IScope scope = ScopeProvider.CreateScope(autoComplete: true);

        var columns = new List<(string Table, string Column)>();
        foreach (string table in new[] { "meresoftBannersBanner", "meresoftBannersBannerVersion", "meresoftBannersZone", "meresoftBannersZoneAssignment", DailyStatDto.TableName, "meresoftBannersRequestedZone" })
        {
            foreach (string column in ColumnNames(scope, table))
            {
                columns.Add((table, column));
            }
        }

        Assert.That(columns.Count, Is.GreaterThan(20), "the schema was read");
        Assert.That(columns.Where(c => forbidden.Contains(c.Column.ToLowerInvariant())), Is.Empty);
    }

    private static IEnumerable<string> ColumnNames(IScope scope, string table) =>
        scope.Database.Fetch<string>($"SELECT name FROM pragma_table_info('{table}')");

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
