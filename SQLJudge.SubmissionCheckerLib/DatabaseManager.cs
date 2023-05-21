using SQLJudge.DatabaseLib;
using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using MySql.Data.MySqlClient;
using Microsoft.Extensions.Configuration;

namespace SQLJudge.SubmissionCheckerLib
{
	public static class DatabaseManager
	{
		public static bool CreateDatabase(IConfiguration configuration, int databaseId, bool forceCreate = false)
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
			}

			(createDatabasePart, createTablesPart) =
				PrepareDbCreationScript(dbCreationScript, dbName);

			using (var db = DatabaseProviderFactory.GetProviderByDbms(dbms,
				configuration.GetConnectionString(dbms)))
			{
				db.ExecuteQuery(createDatabasePart, false);
			}

			using (var db = DatabaseProviderFactory.GetProviderByDbms(dbms,
				configuration.GetConnectionString(dbms) + $"Database={dbName}"))
			{
				db.ExecuteQuery(createTablesPart, false);
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
	}
}
