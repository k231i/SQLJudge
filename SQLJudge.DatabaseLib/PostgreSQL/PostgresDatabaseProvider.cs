using Npgsql;
using System.Data;
using System.Data.Common;

namespace SQLJudge.DatabaseLib.PostgreSQL
{
	public class PostgresDatabaseProvider : DatabaseProvider
	{
		public PostgresDatabaseProvider(string connectionString) 
			: base(connectionString)
		{
			Connection = new NpgsqlConnection(connectionString);
			Connection.Open();
		}

		public override DbTransaction BeginTransaction() =>
			((NpgsqlConnection)Connection).BeginTransaction();

		public override DataSet ExecuteQuery(string query, bool select = true, int timeLimit = 0)
		{
			if (string.IsNullOrEmpty(query))
			{
				throw new ArgumentNullException(nameof(query));
			}

			if (!select)
			{
				var cmd = new NpgsqlCommand(query);

				if (timeLimit > 0)
				{
					cmd.CommandTimeout = timeLimit;
				}

				cmd.ExecuteNonQuery();

				return new DataSet();
			}

			var adapter = new NpgsqlDataAdapter(query, (NpgsqlConnection)Connection);

			if (timeLimit > 0)
			{
				adapter.SelectCommand.CommandTimeout = timeLimit;
			}
			
			var result = new DataSet();
			adapter.Fill(result);

			return result;
		}

		public override bool CheckDatabaseExists(string databaseName)
		{
			var command = new NpgsqlCommand(
				"SELECT 1 FROM pg_database WHERE datname=@dbname",
				(NpgsqlConnection)Connection);
			command.Parameters.AddWithValue("dbname", databaseName);

			return command.ExecuteScalar() != null;
		}
	}
}
