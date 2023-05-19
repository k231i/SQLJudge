namespace SQLJudge.DatabaseLib
{
	public static class DatabaseProviderFactory
	{
		public static DatabaseProvider GetProvider(string providerTypeName, string connectionString) =>
			providerTypeName switch
			{
				"MySqlDatabaseProvider" => new MySQL.MySqlDatabaseProvider(connectionString),
				"PostgresDatabaseProvider" => new PostgreSQL.PostgresDatabaseProvider(connectionString),
				_ => throw new ArgumentOutOfRangeException(nameof(providerTypeName))
			};

		public static DatabaseProvider GetProviderByDbms(string dbms, string connectionString) =>
			dbms switch
			{
				"MySQL" => GetProvider("MySqlDatabaseProvider", connectionString),
				"PostgreSQL" => GetProvider("PostgresDatabaseProvider", connectionString),
				_ => throw new ArgumentOutOfRangeException(nameof(dbms))
			};
	}
}
