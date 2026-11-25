using NUnit.Framework;

/// <summary>
/// Assembly-wide setup (no namespace on purpose, so it applies to every test). It chooses the test
/// database and then runs the setup of the Umbraco test infrastructure, which loads
/// <c>appsettings.Tests.json</c>. SQLite is the default. When the environment variable
/// <see cref="SqlServerEnvironmentVariable"/> holds a connection string to the master database of a
/// SQL Server, the run switches to SQL Server. The connection string is never stored in the repository.
/// </summary>
[SetUpFixture]
public sealed class DatabaseSetup
{
    /// <summary>Name of the environment variable that holds the SQL Server master connection string.</summary>
    public const string SqlServerEnvironmentVariable = "MB_TEST_SQLSERVER";

    private GlobalSetupTeardown? _umbracoSetup;

    /// <summary>True when this run targets SQL Server.</summary>
    public static bool UsesSqlServer { get; private set; }

    [OneTimeSetUp]
    public void SelectDatabase()
    {
        string? connectionString = Environment.GetEnvironmentVariable(SqlServerEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            // The Umbraco test configuration reads these through its environment variable provider.
            Environment.SetEnvironmentVariable("Tests__Database__DatabaseType", "SqlServer");
            Environment.SetEnvironmentVariable("Tests__Database__SQLServerMasterConnectionString", connectionString);
            UsesSqlServer = true;
        }

        _umbracoSetup = new GlobalSetupTeardown();
        _umbracoSetup.SetUp();
    }

    [OneTimeTearDown]
    public void TearDownDatabases() => _umbracoSetup?.TearDown();
}
