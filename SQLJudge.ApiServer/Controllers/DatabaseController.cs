using Microsoft.AspNetCore.Mvc;
using SQLJudge.SubmissionCheckerLib;

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

		[HttpPost("create/{id}")]
		public ActionResult Create(int id)
		{
			if (DatabaseManager.CreateDatabase(_configuration, id))
			{
				return Created("", null);
			}

			return Ok();
		}

		[HttpPost("forcecreate/{id}")]
		public ActionResult ForceCreate(int id)
		{
			DatabaseManager.CreateDatabase(_configuration, id, true);
			return Created("", null);
		}

		[HttpPost("drop")]
		public ActionResult Drop(
			[FromQuery(Name = "databaseIds")]
			IEnumerable<long> databaseIds)
		{
			if (databaseIds is null || !databaseIds.Any())
			{
				return Empty;
			}

			foreach (var databaseId in databaseIds)
			{
				DatabaseManager.DropDatabase(_configuration, databaseId);
			}

			return Ok();
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
