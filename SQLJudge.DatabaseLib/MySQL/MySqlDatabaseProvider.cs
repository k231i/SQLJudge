using MySql.Data;
using MySql.Data.MySqlClient;
using Npgsql;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SQLJudge.DatabaseLib.MySQL
{
	public class MySqlDatabaseProvider : DatabaseProvider
	{
		public MySqlDatabaseProvider(string connectionString) 
			: base(connectionString)
		{
			Connection = new MySqlConnection(connectionString);
		}

		public override DbTransaction BeginTransaction()
			=> ((MySqlConnection)Connection).BeginTransaction();

		public override DataSet ExecuteQuery(string query, bool select = true, int timeLimit = 0)
		{
			if (string.IsNullOrEmpty(query))
			{
				throw new ArgumentNullException(nameof(query));
			}

			if (!select)
			{
				var cmd = new MySqlCommand(query);

				if (timeLimit > 0)
				{
					cmd.CommandTimeout = timeLimit;
				}

				cmd.ExecuteNonQuery();

				return new DataSet();
			}

			var adapter = new MySqlDataAdapter(query, (MySqlConnection)Connection);

			if (timeLimit > 0)
			{
				adapter.SelectCommand.CommandTimeout = timeLimit;
			}

			var result = new DataSet();
			adapter.Fill(result);

			return result;
		}

		public override bool CheckDatabaseExists(string databaseName) => 
			new MySqlCommand($"SHOW DATABASES LIKE '{databaseName}'", (MySqlConnection)Connection)
				.ExecuteScalar() != null;
	}
}
