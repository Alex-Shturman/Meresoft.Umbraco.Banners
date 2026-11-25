using NUnit.Framework;

namespace Meresoft.Umbraco.Banners.Tests.Integration.TestSetup;

/// <summary>
/// Chooses the test database. SQLite is the default (appsettings.Tests.json). When the environment
/// variable <see cref="SqlServerEnvironmentVariable"/> holds a connection string to a SQL Server
/// master database, the Umbraco test infrastructure is switched to SQL Server. The connection
/// string is never stored in the repository.
/// </summary>
[SetUpFixture]
public sealed class DatabaseSetup
{
    public const string SqlServerEnvironmentVariable = "MB_TEST_SQLSERVER";

    /// <summary>True when this run targets SQL Server.</summary>
    public static bool UsesSqlServer { get; private set; }

    [OneTimeSetUp]
    public void SelectDatabase()
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlServerEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        // The Umbraco test host reads these through the standard configuration providers.
        Environment.SetEnvironmentVariable("Tests__Database__DatabaseType", "SqlServer");
        Environment.SetEnvironmentVariable("Tests__Database__SQLServerMasterConnectionString", connectionString);
        UsesSqlServer = true;
    }
}
