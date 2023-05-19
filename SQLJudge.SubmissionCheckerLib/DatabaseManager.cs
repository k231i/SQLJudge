using SQLJudge.DatabaseLib;
using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLJudge.SubmissionCheckerLib
{
	public static class DatabaseManager
	{
		public static bool CreateDatabase(int databaseId, bool forceCreate = false)
		{
			var dbName = $"db{databaseId}";
			string dbms, dbCreationScript;

			using (var db = DatabaseProviderFactory.GetProvider("MySqlDatabaseProvider",
				ConfigurationManager.ConnectionStrings["MoodleDB"].ConnectionString))
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
				ConfigurationManager.ConnectionStrings[dbms].ConnectionString))
			{
				if (db.CheckDatabaseExists(dbName))
				{
					if (!forceCreate)
					{
						return false;
					}

					db.ExecuteQuery($"DROP DATABASE {dbName};", false);
				}

				dbCreationScript = PrepareDbCreationScript(dbCreationScript, dbName);

				db.ExecuteQuery(dbCreationScript, false);

				return true;
			}
		}

		public static string PrepareDbCreationScript(string script, string dbName)
		{
			// replace db name in CREATE DATABASE to dbName




			return script;
		}
	}
}
