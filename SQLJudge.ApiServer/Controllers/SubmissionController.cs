using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SQLJudge.SubmissionCheckerLib;

namespace SQLJudge.ApiServer.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class SubmissionController : ControllerBase
	{
		private readonly ILogger<SubmissionController> _logger;
		private readonly IConfiguration _configuration;

		public SubmissionController(ILogger<SubmissionController> logger, IConfiguration configuration)
		{
			_logger = logger;
			_configuration = configuration;
		}

		[HttpPost("check")]
		public ActionResult CheckSubmissions([FromQuery(Name = "submissionIds")] IEnumerable<int> submissionIds)
		{
			if (submissionIds is null || !submissionIds.Any())
			{
				return Empty;
			}

			var failedSubmissionIds = new List<int>();

			foreach (var submissionId in submissionIds)
			{
				try
				{
					SubmissionChecker.CheckSubmission(_configuration, submissionId);
				}
				catch (Exception ex)
				{
					failedSubmissionIds.Add(submissionId);
					_logger.LogError(ex, "Submission Id: {submissionId}", submissionId);
				}
			}

			if (failedSubmissionIds.Any())
			{
				return UnprocessableEntity(failedSubmissionIds);
			}

			return Ok();
		}

		[HttpPost("correctoutput")]
		public ActionResult GenerateCorrectOutput([FromQuery(Name = "submissionId")] IEnumerable<int> submissionIds)
		{
			if (submissionIds is null || !submissionIds.Any())
			{
				return Empty;
			}

			var failedSubmissionIds = new List<int>();

			foreach (var submissionId in submissionIds)
			{
				try
				{
					SubmissionChecker.GenerateCorrectOutput(_configuration, submissionId);
				}
				catch (Exception ex)
				{
					failedSubmissionIds.Add(submissionId);
					_logger.LogError(ex, "Submission Id: {submissionId}", submissionId);
				}
			}

			if (failedSubmissionIds.Any())
			{
				return UnprocessableEntity(failedSubmissionIds);
			}

			return Ok();
		}
	}
}
