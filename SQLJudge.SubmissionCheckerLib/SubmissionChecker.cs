using System;
using System.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SQLJudge.DatabaseLib;
using Newtonsoft.Json;
using System.Data;

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

		public static void CheckSubmission(int submissionId)
		{
			int dbId, timeLimit, sqljSubmissionId;
			string dbName, dbms, checkScript, correctAnswer, correctOutput, mustContain, input;

			using (var db = DatabaseProviderFactory.GetProvider("MySqlDatabaseProvider",
				ConfigurationManager.ConnectionStrings["MoodleDB"].ConnectionString))
			{
				var select = db.ExecuteQuery($"""
					SELECT jdb.id AS dbid
						, jdb.dbms AS dbms
						, ja.timelimit AS timelimit
						, ja.checkscript AS checkscript
						, ja.correctanswer AS correctanswer
						, ja.correctoutput AS correctoutput
						, ja.mustcontain AS mustcontain
						, t.onlinetext AS input
						, js.id AS sqljsubmissionid
					FROM mdl_database_sqlj jdb
					JOIN mdl_assignment_sqlj ja
						ON jdb.id = ja.database
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

				dbId = (int)select["dbid"];
				dbName = $"db{dbId}";
				dbms = (string)select["dbms"];
				timeLimit = (int)select["timelimit"];
				checkScript = (string)select["checkscript"];
				correctAnswer = (string)select["correctanswer"];
				correctOutput = (string)select["correctoutput"];
				mustContain = (string)select["mustcontain"];
				input = (string)select["input"];
				sqljSubmissionId = (int)select["sqljsubmissionid"];
			}

			if (input.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries)
				.Any(x => x.StartsWith("\\")))
			{
				SetStatus(sqljSubmissionId, SqljSubmissionStatus.ContainsRestrictedFunctions, """
					Answer contains functions starting with \
					""");

				return;
			}

			var mustContainList = new List<string>();
			var mustNotContainList = new List<string>();

			foreach (var item in mustContain
				.Split(new string[] { "\n", "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
			{
				if (item.StartsWith('#'))
				{
					continue;
				}

				if (item.StartsWith('!') && input.Contains(item.Substring(1)))
				{
					mustNotContainList.Add(item.Substring(1));
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
				outputMessage += "Answer contains the following banned keywords/phrases:\n" +
					string.Join("\n", mustNotContainList) + "\n";
			}
			
			if (mustContainList.Any())
			{
				outputMessage += "Answer does not contain the following required keywords/phrases:\n" +
					string.Join("\n", mustContainList) + "\n";
			}

			if (!string.IsNullOrEmpty(outputMessage))
			{
				SetStatus(sqljSubmissionId,
					SqljSubmissionStatus.BannedOrRequiredWordsContent,
					outputMessage.TrimEnd('\n'));
				return;
			}

			using (var db = DatabaseProviderFactory.GetProviderByDbms(dbms,
				ConfigurationManager.ConnectionStrings[dbms].ConnectionString))
			{
				if (!db.CheckDatabaseExists(dbName))
				{
					DatabaseManager.CreateDatabase(dbId);
				}
			}

			if (string.IsNullOrEmpty(correctOutput))
			{
				correctOutput = GenerateCorrectOutput(
					dbName, dbms, correctAnswer, checkScript, sqljSubmissionId);
			}

			var correctOutputResult = JsonConvert.DeserializeObject<DataSet>(correctOutput);
			DataSet inputResult;

			using (var db = DatabaseProviderFactory.GetProviderByDbms(dbms,
				ConfigurationManager.ConnectionStrings[dbms].ConnectionString + $"Database={dbName}"))
			{
				var transaction = db.BeginTransaction();

				try
				{
					inputResult = db.ExecuteQuery(
						correctAnswer + "\n" + checkScript, timeLimit: timeLimit);
				}
				catch
				{
					SetStatus(sqljSubmissionId, SqljSubmissionStatus.TimeLimitExceeded, $"""
						Time limit of {timeLimit} seconds has been exceeded, or an unknown error occured
						""");

					return;
				}
				finally
				{
					transaction.Rollback();
				}
			}

			if (AreDataSetsEqual(correctOutputResult, inputResult))
			{
				SetStatus(sqljSubmissionId, SqljSubmissionStatus.Accepted, "");
				return;
			}

			SetStatus(sqljSubmissionId, SqljSubmissionStatus.WrongAnswer, "");
		}

		public static string GenerateCorrectOutput(
			string dbName, 
			string dbms, 
			string correctAnswer,
			string checkScript,
			int sqljSubmissionId)
		{
			string correctOutput;

			using (var db = DatabaseProviderFactory.GetProviderByDbms(dbms,
				ConfigurationManager.ConnectionStrings[dbms].ConnectionString + $"Database={dbName}"))
			{
				var transaction = db.BeginTransaction();

				var result = db.ExecuteQuery(correctAnswer + "\n" + checkScript);

				transaction.Rollback();

				correctOutput = JsonConvert.SerializeObject(result);
			}

			using (var db = DatabaseProviderFactory.GetProvider("MySqlDatabaseProvider",
				ConfigurationManager.ConnectionStrings["MoodleDB"].ConnectionString))
			{
				db.ExecuteQuery($"""
					UPDATE mdl_assignment_sqlj_submission
					SET output = '{correctOutput.Replace("'", "''")}'
					WHERE id = {sqljSubmissionId}
					""", false);
			}

			return correctOutput;
		}

		public static void SetStatus(int sqljSubmissionId, SqljSubmissionStatus status, string output)
		{
			using (var db = DatabaseProviderFactory.GetProvider("MySqlDatabaseProvider",
				ConfigurationManager.ConnectionStrings["MoodleDB"].ConnectionString))
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

		public static bool AreDataSetsEqual(DataSet ds1, DataSet ds2)
		{
			if (ds1.Tables.Count != ds2.Tables.Count)
				return false;

			for (var i = 0; i < ds1.Tables.Count; i++)
				if (!AreTablesEqual(ds1.Tables[i], ds2.Tables[i]))
					return false;

			return true;
		}

		public static bool AreTablesEqual(DataTable t1, DataTable t2)
		{
			if (t1.Rows.Count != t2.Rows.Count || t1.Columns.Count != t2.Columns.Count)
				return false;

			for (int i = 0; i < t1.Rows.Count; i++)
				for (int c = 0; c < t1.Columns.Count; c++)
					if (!Equals(t1.Rows[i][c], t2.Rows[i][c]))
						return false;

			return true;
		}
	}
}
