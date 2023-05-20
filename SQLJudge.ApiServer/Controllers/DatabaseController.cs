using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using SQLJudge.SubmissionCheckerLib;
using System.Configuration;

namespace SQLJudge.ApiServer.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class DatabaseController : ControllerBase
	{
		private readonly ILogger<DatabaseController> _logger;
		private readonly IConfiguration _configuration;

		public DatabaseController(ILogger<DatabaseController> logger, IConfiguration configuration)
		{
			_logger = logger;
			_configuration = configuration;
		}

		[HttpGet("create/{databaseId:int}")]
		public ActionResult Create(int databaseId)
		{
			if (DatabaseManager.CreateDatabase(databaseId))
			{
				return Created("", null);
			}

			return Ok();
		}

		[HttpGet("forcecreate/{databaseId:int}")]
		public ActionResult ForceCreate(int databaseId)
		{
			DatabaseManager.CreateDatabase(databaseId, true);
			return Created("", null);
		}

		[HttpGet("dbmslist")]
		public ActionResult<IEnumerable<string>> DbmsList()
		{
			var result = new List<string>();
			
			foreach (var s in _configuration.GetSection("ConnectionStrings").GetChildren())
			{
				if (s.Key != "MoodleDB")
				{
					result.Add(s.Key);
				}
			}

			return result;
		}
	}
}
