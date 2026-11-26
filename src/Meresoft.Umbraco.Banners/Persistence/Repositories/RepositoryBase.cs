using NPoco;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Persistence.SqlSyntax;
using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Persistence.Repositories;

/// <summary>Gives repositories the database of the ambient Umbraco scope; the caller owns the scope and the transaction.</summary>
internal abstract class RepositoryBase
{
    private readonly IScopeAccessor _scopeAccessor;

    protected RepositoryBase(IScopeAccessor scopeAccessor)
    {
        _scopeAccessor = scopeAccessor ?? throw new ArgumentNullException(nameof(scopeAccessor));
    }

    /// <summary>The database of the ambient scope.</summary>
    protected IUmbracoDatabase Database =>
        _scopeAccessor.AmbientScope?.Database
        ?? throw new InvalidOperationException("A repository was used outside an Umbraco scope. Create a scope first.");

    /// <summary>A new SQL builder: it quotes names for the database in use (SQL Server or SQLite).</summary>
    protected Sql<ISqlContext> NewSql() => Database.SqlContext.Sql();

    /// <summary>The SQL syntax of the database in use, for the few statements the builder cannot express.</summary>
    protected ISqlSyntaxProvider Syntax => Database.SqlContext.SqlSyntax;

    /// <summary>Throws when an update or delete changed no row: the item was deleted meanwhile or never existed.</summary>
    protected static void RequireRow(int rowsAffected, string what, object id)
    {
        if (rowsAffected == 0)
        {
            throw new InvalidOperationException($"{what} {id} does not exist.");
        }
    }

    /// <summary>Marks a value read from the database as UTC (SQLite returns it without a kind).</summary>
    protected static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    /// <summary>Same as <see cref="Utc(DateTime)"/> for nullable values.</summary>
    protected static DateTime? Utc(DateTime? value) => value is null ? null : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc);
}
