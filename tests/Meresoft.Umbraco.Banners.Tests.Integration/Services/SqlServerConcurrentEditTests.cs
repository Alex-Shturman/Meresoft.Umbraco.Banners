using Meresoft.Umbraco.Banners.Domain;
using Meresoft.Umbraco.Banners.Persistence.Repositories;
using Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;
using NUnit.Framework;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Tests.Integration.Services;

/// <summary>
/// Real simultaneity of two editors, with two connections. It runs only against SQL Server (the run that sets
/// <c>MB_TEST_SQLSERVER</c>, category <c>SqlServer</c>): the SQLite test database stalls on overlapping write scopes.
/// Editor A updates the banner row and holds the transaction. Editor B's guarded update blocks on that row; when A
/// commits, B's update no longer matches (the update date changed) and reports no row, which is ChangedByAnotherUser.
/// Every wait has a timeout, so a broken guard fails the test instead of hanging it.
/// </summary>
[TestFixture]
[Category("SqlServer")]
public class SqlServerConcurrentEditTests : BannersServiceTestBase
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    [OneTimeSetUp]
    public void RequireSqlServer()
    {
        if (!DatabaseSetup.UsesSqlServer)
        {
            Assert.Ignore($"Needs a SQL Server: set {DatabaseSetup.SqlServerEnvironmentVariable} to a connection string of its master database.");
        }
    }

    private static Task<T> OnItsOwnThread<T>(Func<T> work)
    {
        // Without the test's execution context the thread has no ambient scope, like a separate request.
        using (ExecutionContext.SuppressFlow())
        {
            return Task.Run(work);
        }
    }

    [Test]
    public async Task AGuardedUpdateThatWaitsForAnotherEditorsCommit_FindsNoRow()
    {
        BannerZone zone = await NewZoneAsync("racing");
        Banner created = await NewBannerAsync(ImageDraft(NewMediaKey(), zone.Key));
        IBannerRepository repository = GetRequiredService<IBannerRepository>();
        DateTime loaded = created.UpdateDate;

        Banner Load()
        {
            using (ScopeProvider.CreateScope(autoComplete: true))
            {
                return repository.GetByKey(created.Key)!;
            }
        }

        using var aHasWritten = new ManualResetEventSlim();
        using var aMayCommit = new ManualResetEventSlim();

        Task<bool> editorA = OnItsOwnThread(() =>
        {
            Banner mine = Load();
            mine.Name = "Editor A";
            mine.UpdateDate = loaded.AddMinutes(1);
            using IScope scope = ScopeProvider.CreateScope();
            bool saved = repository.SaveDraft(mine, loaded);
            aHasWritten.Set();
            Assert.That(aMayCommit.Wait(Limit), Is.True, "editor A was never told to commit");
            scope.Complete();
            return saved;
        });
        Assert.That(aHasWritten.Wait(Limit), Is.True, "editor A never wrote");

        Task<bool> editorB = OnItsOwnThread(() =>
        {
            Banner mine = Load();
            mine.Name = "Editor B";
            mine.UpdateDate = loaded.AddMinutes(2);
            using IScope scope = ScopeProvider.CreateScope();
            bool saved = repository.SaveDraft(mine, loaded);
            if (saved)
            {
                scope.Complete();
            }

            return saved;
        });

        await Task.Delay(TimeSpan.FromSeconds(1));
        Assert.That(editorB.IsCompleted, Is.False, "editor B's update should wait for the row lock of editor A");

        aMayCommit.Set();

        Assert.That(await editorA.WaitAsync(Limit), Is.True);
        Assert.That(await editorB.WaitAsync(Limit), Is.False, "editor B must find no row once A has committed");
        using (ScopeProvider.CreateScope(autoComplete: true))
        {
            Assert.That(repository.GetByKey(created.Key)!.Name, Is.EqualTo("Editor A"));
        }
    }
}
