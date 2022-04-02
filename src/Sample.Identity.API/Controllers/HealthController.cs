﻿using System.Net;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Swashbuckle.AspNetCore.Annotations;

namespace Sample.Identity.API.Controllers
{
    [Route("api/Health")]
    public class HealthController : Controller
    {
        private readonly HealthCheckService healthCheckService;

        public HealthController(HealthCheckService healthCheckService)
        {
            this.healthCheckService = healthCheckService;
        }

        /// <summary>
        ///     Get Health
        /// </summary>
        /// <remarks>Provides an indication about the health of the API</remarks>
        /// <response code="200">API is healthy</response>
        /// <response code="503">API is unhealthy or in degraded state</response>
        [HttpGet]
        [ProducesResponseType((int)HttpStatusCode.OK)]
        [ProducesResponseType((int)HttpStatusCode.ServiceUnavailable)]
        [SwaggerOperation(OperationId = "Health_Get")]
        public async Task<IActionResult> Get()
        {
            HealthReport report = await healthCheckService.CheckHealthAsync();

            return report.Status == HealthStatus.Healthy ? Ok(report) : StatusCode((int)HttpStatusCode.ServiceUnavailable, report);
        }
    }
}