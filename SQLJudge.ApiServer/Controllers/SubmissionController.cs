﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SQLJudge.SubmissionCheckerLib;

namespace SQLJudge.ApiServer.Controllers
{
	[Route("api/[controller]")]
	[ApiController]
	public class SubmissionController : ControllerBase
	{
		private readonly ILogger<SubmissionController> _logger;

		public SubmissionController(ILogger<SubmissionController> logger)
		{
			_logger = logger;
		}

		public ActionResult CheckSubmissions(IEnumerable<int> submissionIds)
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
					SubmissionChecker.CheckSubmission(submissionId);
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
