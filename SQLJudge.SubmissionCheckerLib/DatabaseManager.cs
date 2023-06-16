using Microsoft.Extensions.Configuration;
using SQLJudge.DatabaseLib;
using SQLJudge.DatabaseLib.PostgreSQL;
using System.Text.RegularExpressions;

namespace SQLJudge.SubmissionCheckerLib
{
	public static class DatabaseManager
	{
		public static bool CreateDatabase(IConfiguration configuration, long databaseId, bool forceCreate = false)
		{
			var dbName = $"db{databaseId}";
			string dbms, dbCreationScript, createDatabasePart, createTablesPart;

			using (var db = DatabaseProviderFactory.GetProvider("MySqlDatabaseProvider",
				configuration.GetConnectionString("MoodleDB")))
			{
				var select = db.ExecuteQuery($"""
					SELECT dbms
						, dbcreationscript 
					FROM mdl_database_sqlj 
					WHERE id = {databaseId};
					""");

				dbms = (string)select.Tables[0].Rows[0]["dbms"];
				dbCreationScript = (string)select.Tables[0].Rows[0]["dbcreationscript"];
			}

			using (var db = DatabaseProviderFactory.GetProviderByDbms(dbms,
				configuration.GetConnectionString(dbms)))
			{
				if (db.CheckDatabaseExists(dbName))
				{
					if (!forceCreate)
					{
						return false;
					}

					db.ExecuteQuery($"DROP DATABASE {dbName};", false);
				}

				(createDatabasePart, createTablesPart) =
					PrepareDbCreationScript(dbCreationScript, dbName);

				db.ExecuteQuery(createDatabasePart, false);
			}

			using (var db = DatabaseProviderFactory.GetProviderByDbms(dbms,
				configuration.GetConnectionString(dbms) + $"Database={dbName}"))
			{
				foreach (var statement in createTablesPart.Split("\r\n\r\n",
					StringSplitOptions.RemoveEmptyEntries))
				{
					if (statement.StartsWith("COPY", StringComparison.CurrentCultureIgnoreCase))
					{
						((PostgresDatabaseProvider)db).Copy(statement);
					}
					else
					{
						db.ExecuteQuery(statement, false);
					}
				}
			}

			return true;
		}

		public static (string, string) PrepareDbCreationScript(string script, string dbName)
		{
			string createDatabasePart = $"CREATE DATABASE {dbName};";

			if (!script.Contains("CREATE DATABASE", StringComparison.CurrentCultureIgnoreCase))
			{
				return (createDatabasePart, script);
			}

			return (createDatabasePart, Regex.Replace(script, 
				@"(CREATE\s+DATABASE.*?;|\\c.*?;|USE\s+\w+;)", "", 
				RegexOptions.IgnoreCase | RegexOptions.Multiline));
		}

		public static void DropDatabase(IConfiguration configuration, long databaseId)
		{
			var dbName = $"db{databaseId}";
			string dbms;

			using (var db = DatabaseProviderFactory.GetProvider("MySqlDatabaseProvider",
				configuration.GetConnectionString("MoodleDB")))
			{
				var select = db.ExecuteQuery($"""
					SELECT dbms
					FROM mdl_database_sqlj 
					WHERE id = {databaseId};
					""");

				dbms = (string)select.Tables[0].Rows[0]["dbms"];
			}

			using (var db = DatabaseProviderFactory.GetProviderByDbms(dbms,
				configuration.GetConnectionString(dbms)))
			{
				db.ExecuteQuery($"DROP DATABASE {dbName};", false);
			}
		}
	}
}
