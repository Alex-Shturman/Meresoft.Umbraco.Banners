using Meresoft.Umbraco.Banners.ManagementApi.History;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using NSubstitute;
using NUnit.Framework;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.Membership;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Unit.ManagementApi;

[TestFixture]
public class BannerHistoryReaderTests
{
    private static readonly Guid KeyOfFirst = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000001");
    private static readonly Guid KeyOfSecond = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private FakeHistory _history = null!;
    private IUserService _users = null!;
    private IUserIdKeyResolver _resolver = null!;
    private BannerHistoryReader _reader = null!;

    [SetUp]
    public void SetUp()
    {
        _history = new FakeHistory();
        _users = Substitute.For<IUserService>();
        _resolver = Substitute.For<IUserIdKeyResolver>();
        IScopeProvider scopes = Substitute.For<IScopeProvider>();
        scopes.CreateScope(autoComplete: true).ReturnsForAnyArgs(Substitute.For<IScope>());
        _reader = new BannerHistoryReader(_history, scopes, _users, _resolver);
    }

    /// <summary>A hand written fake: NSubstitute cannot proxy an internal interface of the package.</summary>
    private sealed class FakeHistory : IAuditHistoryRepository
    {
        public (long Total, IReadOnlyList<AuditHistoryRow> Rows) Result { get; set; }

        public (int Id, string Type, int Skip, int Take)? Asked { get; private set; }

        public (long Total, IReadOnlyList<AuditHistoryRow> Rows) GetPage(int entityId, string entityType, int skip, int take)
        {
            Asked = (entityId, entityType, skip, take);
            return Result;
        }
    }

    private static IUser User(Guid key, string name)
    {
        IUser user = Substitute.For<IUser>();
        user.Key.Returns(key);
        user.Name.Returns(name);
        return user;
    }

    [Test]
    public async Task Users_AreLoadedWithOneQuery_AndAnIdWithNoUserGivesANullName()
    {
        _history.Result = (3L,
        [
            new AuditHistoryRow(1, "Publish", new DateTime(2027, 3, 1, 10, 0, 0, DateTimeKind.Utc)),
            new AuditHistoryRow(2, "Save", new DateTime(2027, 3, 1, 9, 0, 0, DateTimeKind.Utc)),
            new AuditHistoryRow(3, "New", new DateTime(2027, 3, 1, 8, 0, 0, DateTimeKind.Utc)),
            new AuditHistoryRow(1, "New", new DateTime(2027, 3, 1, 7, 0, 0, DateTimeKind.Utc)),
        ]);
        _resolver.TryGetAsync(1).Returns(Attempt.Succeed(KeyOfFirst));
        _resolver.TryGetAsync(2).Returns(Attempt.Succeed(KeyOfSecond));
        _resolver.TryGetAsync(3).Returns(Attempt.Fail<Guid>());
        _users.GetAsync(Arg.Any<IEnumerable<Guid>>()).Returns(call => Task.FromResult<IEnumerable<IUser>>(
            [User(KeyOfFirst, "Invented First"), User(KeyOfSecond, "Invented Second")]));

        HistoryPageModel page = await _reader.ReadAsync(5, "meresoft-banner", 0, 10);

        Assert.Multiple(() =>
        {
            Assert.That(page.Total, Is.EqualTo(3), "the total comes from the database filter, not from the rows of the page");
            Assert.That(page.Items.Select(i => i.UserName), Is.EqualTo(new string?[] { "Invented First", "Invented Second", null, "Invented First" }));
            Assert.That(page.Items.Select(i => i.Action), Is.EqualTo(new[] { "publish", "save", "new", "new" }));
            Assert.That(page.Items[0].Date, Is.EqualTo(new DateTime(2027, 3, 1, 10, 0, 0, DateTimeKind.Utc)));
        });
        Assert.That(_history.Asked, Is.EqualTo((5, "meresoft-banner", 0, 10)));
        _ = _users.Received(1).GetAsync(Arg.Any<IEnumerable<Guid>>());
    }
}
