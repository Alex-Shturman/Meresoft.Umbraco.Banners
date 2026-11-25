using Umbraco.Cms.Infrastructure.Scoping;

namespace Meresoft.Umbraco.Banners.Persistence;

/// <summary>Takes the package's write lock for a scope, and says clearly what is wrong when the lock row is not there.</summary>
internal static class PackageWriteLock
{
    /// <summary>
    /// Takes <see cref="BannersConstants.WriteLockId"/> at once. Throws an <see cref="InvalidOperationException"/> that names the
    /// package migration when the row of the lock is missing in <c>umbracoLock</c>, which means the migration has not run.
    /// </summary>
    public static void TakeWriteLock(this IScope scope)
    {
        try
        {
            scope.EagerWriteLock(BannersConstants.WriteLockId);
        }
        catch (ArgumentException ex) when (ex.Message.Contains(BannersConstants.WriteLockId.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"The Meresoft.Umbraco.Banners write lock (id {BannersConstants.WriteLockId}) is missing from the umbracoLock table, "
                + $"so the package cannot write. The package migration '{BannersConstants.MigrationPlanName}' has not run. "
                + "Run the Umbraco package migrations (start the site with unattended package migrations on, or run the migration from the back office) and try again.",
                ex);
        }
    }
}
