using backend.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;

namespace backend.Tests.Controllers;

public class StatusControllerTests
{
    [Fact]
    public void GetStatus_ShouldReturnOk()
    {
        var controller = new StatusController();

        var result = controller.GetStatus();

        result.Should().BeOfType<OkObjectResult>();
    }
}
