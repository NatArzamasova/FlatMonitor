using FlatMonitor.Api.Contracts;
using FlatMonitor.Api.Controllers;
using FlatMonitor.Models;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using NUnit.Framework;

namespace FlatMonitor.Tests.Api.Controllers;

[TestFixture]
public class DistrictsControllerTests
{
    [Test]
    public void GetAll_ReturnsAllKarlsruheDistricts()
    {
        var result = new DistrictsController().GetAll();

        var objectResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        var response = objectResult.Value.Should().BeOfType<ApiResponse<List<string>>>().Subject;
        response.Success.Should().BeTrue();
        response.Message.Should().Be("Districts fetched successfully");
        response.Data.Should().HaveCount(27).And.BeEquivalentTo(KarlsruheDistricts.All);
    }

    [Test]
    public void GetAll_ReturnsDistrictsSortedByName()
    {
        var response = (ApiResponse<List<string>>)((OkObjectResult)new DistrictsController().GetAll().Result!).Value!;

        response.Data.Should().StartWith("Beiertheim-Bulach").And.EndWith("Wolfartsweier");
        response.Data.Should().BeInAscendingOrder(StringComparer.InvariantCultureIgnoreCase);
    }
}
