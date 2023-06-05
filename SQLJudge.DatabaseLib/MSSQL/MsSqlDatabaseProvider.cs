using Microsoft.Data.SqlClient;
using System.Data;
using System.Data.Common;

namespace SQLJudge.DatabaseLib.MSSQL
{
	public class MsSqlDatabaseProvider : DatabaseProvider
	{
		public MsSqlDatabaseProvider(string connectionString)
			: base(connectionString)
		{
			Connection = new SqlConnection(connectionString);
			Connection.Open();
		}

		public override DbTransaction BeginTransaction()
			=> ((SqlConnection)Connection).BeginTransaction();

		public override DataSet ExecuteQuery(string query, bool select = true, int timeLimit = 0)
		{
			if (string.IsNullOrEmpty(query))
			{
				throw new ArgumentNullException(nameof(query));
			}

			if (!select)
			{
				var cmd = new SqlCommand(query, (SqlConnection)Connection);

				if (timeLimit > 0)
				{
					cmd.CommandTimeout = timeLimit;
				}

				cmd.ExecuteNonQuery();

				return new DataSet();
			}

			var adapter = new SqlDataAdapter(query, (SqlConnection)Connection);

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
			var command = new SqlCommand("""
				SELECT 1 
				FROM master.dbo.databases 
				WHERE ('[' + name + ']' = @dbname 
				OR name = @dbname
				""",
				(SqlConnection)Connection);
			command.Parameters.AddWithValue("dbname", databaseName);

			return command.ExecuteScalar() != null;
		}
	}
}
