using System.Data;
using System.Data.Common;

namespace SQLJudge.DatabaseLib
{
	public abstract class DatabaseProvider : IDisposable
	{
		protected string ConnectionString;

		protected DbConnection Connection;

		public DatabaseProvider(string connectionString)
		{
			if (string.IsNullOrEmpty(connectionString))
			{
				throw new ArgumentNullException(nameof(connectionString));
			}

			ConnectionString = connectionString;
		}

		public abstract DbTransaction BeginTransaction();

		public abstract DataSet ExecuteQuery(string query, bool select = true, int timeLimit = 0);

		public abstract bool CheckDatabaseExists(string databaseName);

		public void Dispose()
		{
			Connection.Close();
		}
	}
}
