using backend.Controllers;
using backend.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Controllers;

public class TokenControllerTests
{
    [Fact]
    public void RevokeToken_ShouldReturnBadRequest_WhenHeaderMissing()
    {
        var controller = new TokenController(new AdminTokenService(), NullLogger<TokenController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };

        var result = controller.RevokeToken();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void RevokeToken_ShouldReturnNotFound_WhenTokenUnknown()
    {
        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Admin-Token"] = "missing-token";

        var controller = new TokenController(new AdminTokenService(), NullLogger<TokenController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };

        var result = controller.RevokeToken();

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void RevokeToken_ShouldReturnOk_WhenTokenExists()
    {
        var service = new AdminTokenService();
        var token = service.GenerateToken(1);

        var ctx = new DefaultHttpContext();
        ctx.Request.Headers["Admin-Token"] = token;

        var controller = new TokenController(service, NullLogger<TokenController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = ctx }
        };

        var result = controller.RevokeToken();

        result.Should().BeOfType<OkObjectResult>();
        service.ValidateToken(token).Should().BeNull();
    }
}
