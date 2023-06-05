using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using SQLJudge.DatabaseLib;
using System.Data;
using System.Text;
using System.Text.RegularExpressions;

namespace SQLJudge.SubmissionCheckerLib
{
	public static class SubmissionChecker
	{
		public enum SqljSubmissionStatus
		{
			Pending = 0,
			Accepted = 1,
			WrongAnswer = 2,
			BannedOrRequiredWordsContent = 3,
			ContainsRestrictedFunctions = 4,
			TimeLimitExceeded = 5,
			UnknownError = 6
		}

		public static void CheckSubmission(IConfiguration configuration, long submissionId)
		{
			#region prepare
			long dbId, sqljSubmissionId, assignId;
			int timeLimit;
			string dbName, dbms, checkScript, correctAnswer, correctOutput, mustContain, input;

			using (var db = DatabaseProviderFactory.GetProvider("MySqlDatabaseProvider",
				configuration.GetConnectionString("MoodleDB")))
			{
				var select = db.ExecuteQuery($"""
					SELECT jdb.id AS dbid
						, jdb.dbms AS dbms
						, ja.timelimit AS timelimit
						, ja.checkscript AS checkscript
						, ja.correctanswer AS correctanswer
						, ja.correctoutput AS correctoutput
						, ja.mustcontain AS mustcontain
						, a.id AS assignid
						, t.onlinetext AS input
						, js.id AS sqljsubmissionid
					FROM mdl_database_sqlj jdb
					JOIN mdl_assignment_sqlj ja
						ON jdb.id = ja.testdb
					JOIN mdl_assign a
						ON ja.assignment = a.id
					JOIN mdl_assign_submission s
						ON a.id = s.assignment
					JOIN mdl_assignsubmission_onlinetext t
						ON s.id = t.submission
					JOIN mdl_assignment_sqlj_submission js
						ON s.id = js.submission
					WHERE s.id = {submissionId}
						AND t.onlinetext IS NOT NULL
						AND t.onlinetext <> '';
					""").Tables[0].Rows[0];

				dbId = (long)select["dbid"];
				dbName = $"db{dbId}";
				dbms = select["dbms"].ToString();
				timeLimit = (int)select["timelimit"];
				checkScript = select["checkscript"].ToString();
				correctAnswer = select["correctanswer"].ToString();
				correctOutput = select["correctoutput"] == DBNull.Value 
					? null
					: select["correctoutput"].ToString();
				mustContain = select["mustcontain"].ToString();
				assignId = (long)select["assignid"];
				input = Regex.Replace(select["input"].ToString(), "<.*?>", " ");
				sqljSubmissionId = (long)select["sqljsubmissionid"];
			}
			#endregion

			#region check restricted functions
			if (input.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
				.Any(x => x.StartsWith("\\")))
			{
				SetStatus(configuration, sqljSubmissionId, SqljSubmissionStatus.ContainsRestrictedFunctions, """
					<h5>Answer contains functions starting with \</h5>
					""");

				return;
			}
			#endregion

			#region check banned/required keywords
			var mustContainList = new List<string>();
			var mustNotContainList = new List<string>();

			foreach (var item in mustContain
				.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
			{
				if (item.StartsWith('#'))
				{
					continue;
				}

				if (item.StartsWith('!'))
				{
					if (input.Contains(item.Substring(1)))
					{
						mustNotContainList.Add(item.Substring(1));
					}

					continue;
				}

				if (!input.Contains(item))
				{
					mustContainList.Add(item);
				}
			}

			var outputMessage = "";

			if (mustNotContainList.Any())
			{
				outputMessage += $"""
					<h5>Answer contains the following <u>banned</u> keywords/phrases:</h5>
					<pre>
					{string.Join("\n", mustNotContainList)}
					</pre>
					""";
			}
			
			if (mustContainList.Any())
			{
				outputMessage += $"""
					<h5>Answer does not contain the following <u>required</u> keywords/phrases:</h5>
					<pre>
					{string.Join("\n", mustContainList)}
					</pre>
					""";
			}

			if (!string.IsNullOrEmpty(outputMessage))
			{
				SetStatus(configuration, sqljSubmissionId,
					SqljSubmissionStatus.BannedOrRequiredWordsContent,
					outputMessage);
				return;
			}
			#endregion

			#region create db and/or correct output if needed
			using (var db = DatabaseProviderFactory.GetProviderByDbms(dbms,
				configuration.GetConnectionString(dbms)))
			{
				if (!db.CheckDatabaseExists(dbName))
				{
					DatabaseManager.CreateDatabase(configuration, dbId);
				}
			}

			if (string.IsNullOrEmpty(correctOutput))
			{
				correctOutput = GenerateCorrectOutput(
					configuration, dbName, dbms, correctAnswer, checkScript, assignId);
			}
			else if (correctOutput != GenerateCorrectOutput(
				configuration, dbName, dbms, correctAnswer, checkScript, assignId))
			{
				using (var db = DatabaseProviderFactory.GetProviderByDbms(dbms,
					configuration.GetConnectionString(dbms)))
				{
					DatabaseManager.CreateDatabase(configuration, dbId, true);
				}

				correctOutput = GenerateCorrectOutput(
					configuration, dbName, dbms, correctAnswer, checkScript, assignId);
			}
			#endregion

			#region run input script
			var correctOutputResult = JsonConvert.DeserializeObject<DataSet>(correctOutput);
			DataSet inputResult;

			using (var db = DatabaseProviderFactory.GetProviderByDbms(dbms,
				configuration.GetConnectionString(dbms) + $"Database={dbName}"))
			{
				var transaction = db.BeginTransaction();

				try
				{
					inputResult = db.ExecuteQuery(
						input + "\n" + checkScript, timeLimit: timeLimit);
				}
				catch
				{
					SetStatus(configuration, sqljSubmissionId, SqljSubmissionStatus.TimeLimitExceeded, $"""
						<h5>Time limit of {timeLimit} seconds has been exceeded, or an unknown error occured</h5>
						""");

					return;
				}
				finally
				{
					transaction.Rollback();
				}
			}

			// to get rid of boxed long/int/short comparisons
			inputResult = JsonConvert.DeserializeObject<DataSet>(JsonConvert.SerializeObject(inputResult));
			#endregion

			#region compare result with correct one
			if (correctOutputResult.Tables.Count != inputResult.Tables.Count)
			{
				SetStatus(configuration, sqljSubmissionId, SqljSubmissionStatus.WrongAnswer, $"""
					<h5>Incorrect number of tables</h5>
					<p>Expected: {correctOutputResult.Tables.Count}</p>
					<p>Actual: {inputResult.Tables.Count}</p>
					""");
				return;
			}

			for (var t = 0; t < correctOutputResult.Tables.Count; t++)
			{
				if (correctOutputResult.Tables[t].Columns.Count !=
					inputResult.Tables[t].Columns.Count)
				{
					SetStatus(configuration, sqljSubmissionId, SqljSubmissionStatus.WrongAnswer, $"""
						<h5>Incorrect number of columns in table {t + 1}</h5>
						<p>Expected: {correctOutputResult.Tables[t].Columns.Count}</p>
						{DataTableToHtml(correctOutputResult.Tables[t])}
						<p>Actual: {inputResult.Tables[t].Columns.Count}</p>
						{DataTableToHtml(inputResult.Tables[t])}
						""");
					return;
				}

				if (correctOutputResult.Tables[t].Rows.Count !=
					inputResult.Tables[t].Rows.Count)
				{
					SetStatus(configuration, sqljSubmissionId, SqljSubmissionStatus.WrongAnswer, $"""
						<h5>Incorrect number of rows in table {t + 1}</5>
						<p>Expected: {correctOutputResult.Tables[t].Rows.Count}</p>
						{DataTableToHtml(correctOutputResult.Tables[t])}
						<p>Actual: {inputResult.Tables[t].Rows.Count}</p>
						{DataTableToHtml(inputResult.Tables[t])}
						""");
					return;
				}

				for (int r = 0; r < correctOutputResult.Tables[t].Rows.Count; r++)
				{
					for (int c = 0; c < correctOutputResult.Tables[t].Columns.Count; c++)
					{
						if (!correctOutputResult.Tables[t].Rows[r][c].Equals(inputResult.Tables[t].Rows[r][c]))
						{
							SetStatus(configuration, sqljSubmissionId, SqljSubmissionStatus.WrongAnswer, $"""
								<h5>Wrong answer in table {t + 1}</h5>
								<p>Correct result</p>
								{DataTableToHtml(correctOutputResult.Tables[t])}
								<p>Your result</p>
								{DataTableToHtml(inputResult.Tables[t])}
								""");
							return;
						}
					}
				}
			}
			#endregion

			SetStatus(configuration, sqljSubmissionId, SqljSubmissionStatus.Accepted, "");
		}

		public static string GenerateCorrectOutput(
			IConfiguration configuration,
			string dbName, 
			string dbms, 
			string correctAnswer,
			string checkScript,
			long assignId)
		{
			string correctOutput;

			using (var db = DatabaseProviderFactory.GetProviderByDbms(dbms,
				configuration.GetConnectionString(dbms) + $"Database={dbName}"))
			{
				var transaction = db.BeginTransaction();

				var result = db.ExecuteQuery(correctAnswer + "\n" + checkScript);

				transaction.Rollback();

				correctOutput = JsonConvert.SerializeObject(result);
			}

			using (var db = DatabaseProviderFactory.GetProvider("MySqlDatabaseProvider",
				configuration.GetConnectionString("MoodleDB")))
			{
				db.ExecuteQuery($"""
					UPDATE mdl_assignment_sqlj
					SET correctoutput = '{correctOutput.Replace("'", "''")}'
					WHERE id = {assignId}
					""", false);
			}

			return correctOutput;
		}

		public static void GenerateCorrectOutput(IConfiguration configuration, long assignId)
		{
			string correctAnswer, checkScript, dbName, dbms;

			using (var db = DatabaseProviderFactory.GetProvider("MySqlDatabaseProvider",
				configuration.GetConnectionString("MoodleDB")))
			{
				var select = db.ExecuteQuery($"""
					SELECT ja.correctanswer AS correctanswer
						, ja.checkscript AS checkscript
						, jdb.id AS dbid
						, jdb.dbms AS dbms
					FROM mdl_assignment_sqlj ja
					JOIN mdl_database_sqlj jdb
						ON jdb.id = ja.testdb
					WHERE ja.assignment = {assignId};
					""").Tables[0].Rows[0];

				correctAnswer = select["correctanswer"].ToString();
				checkScript = select["checkscript"].ToString();
				dbName = $"db{(long)select["dbid"]}";
				dbms = select["dbms"].ToString();
			}

			GenerateCorrectOutput(configuration, dbName, dbms, correctAnswer, checkScript, assignId);
		}

		public static void SetStatus(
			IConfiguration configuration, 
			long sqljSubmissionId, 
			SqljSubmissionStatus status, 
			string output)
		{
			using (var db = DatabaseProviderFactory.GetProvider("MySqlDatabaseProvider",
				configuration.GetConnectionString("MoodleDB")))
			{
				db.ExecuteQuery($"""
					UPDATE mdl_assignment_sqlj_submission
					SET status = {(int)status}
						,output = '{output.Replace("'", "''")}'
						,testedon = {(int)DateTime.UtcNow.Subtract(DateTime.UnixEpoch).TotalSeconds}
					WHERE id = {sqljSubmissionId};
					""", false);
			}
		}

		public static string DataTableToHtml(DataTable table)
		{
			var sb = new StringBuilder();

			sb.Append("<table>");

			sb.Append("<tr>");
			for (int i = 0; i < table.Columns.Count; i++)
				sb.Append($"<td><b>{table.Columns[i].ColumnName}</b></td>");
			sb.Append("</tr>");
			
			for (int i = 0; i < table.Rows.Count; i++)
			{
				sb.Append("<tr>");
				for (int j = 0; j < table.Columns.Count; j++)
					sb.Append($"<td>{table.Rows[i][j]}</td>");
				sb.Append("</tr>");
			}

			sb.Append("</table>");

			return sb.ToString();
		}
	}
}
