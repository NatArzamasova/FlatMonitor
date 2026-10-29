namespace FlatMonitor.Api.Controllers;

using FlatMonitor.Api.Contracts;
using FlatMonitor.Models;
using Microsoft.AspNetCore.Mvc;

[ApiController]
[Route("api/districts")]
public class DistrictsController : ApiControllerBase
{
    /// <summary>All districts the monitor tracks, e.g. for editing filter presets without any flat data.</summary>
    [HttpGet]
    public ActionResult<ApiResponse<List<string>>> GetAll()
    {
        var districts = KarlsruheDistricts.All.Order(StringComparer.InvariantCultureIgnoreCase).ToList();
        return Ok(Success(districts, "Districts fetched successfully"));
    }
}
