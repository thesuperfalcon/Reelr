#:package Microsoft.Data.SqlClient@6.1.6

// Gives the app's managed identities their database users. Run by infra/deploy.ps1:
//   dotnet run infra/sql-users.cs -- <server>.database.windows.net <database> <backend identity> <migrate identity>
// It signs in as the Azure CLI user, who is the server's Entra administrator. Safe to run again.
using Microsoft.Data.SqlClient;

if (args.Length != 4)
{
    Console.Error.WriteLine("Usage: sql-users.cs <server> <database> <backend identity> <migrate identity>");
    return 1;
}

var (server, database, backend, migrate) = (args[0], args[1], args[2], args[3]);

// The backend reads and writes rows; only the migration job may change the schema.
var users = new (string Name, string[] Roles)[]
{
    (backend, ["db_datareader", "db_datawriter"]),
    (migrate, ["db_owner"])
};

var connectionString = new SqlConnectionStringBuilder
{
    DataSource = $"tcp:{server},1433",
    InitialCatalog = database,
    Authentication = SqlAuthenticationMethod.ActiveDirectoryDefault,
    Encrypt = SqlConnectionEncryptOption.Mandatory,
    // A paused database takes up to a minute to wake up.
    ConnectTimeout = 120
}.ConnectionString;

await using var connection = new SqlConnection(connectionString);
await connection.OpenAsync();

foreach (var (name, roles) in users)
{
    var user = new SqlCommandBuilder().QuoteIdentifier(name);
    var sql = $"""
        IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = @name)
            CREATE USER {user} FROM EXTERNAL PROVIDER;
        {string.Join("\n", roles.Select(role => $"ALTER ROLE {role} ADD MEMBER {user};"))}
        """;

    await using var command = new SqlCommand(sql, connection);
    command.Parameters.AddWithValue("@name", name);
    await command.ExecuteNonQueryAsync();
    Console.WriteLine($"{name}: {string.Join(", ", roles)}");
}

return 0;
