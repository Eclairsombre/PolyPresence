using backend.Controllers;
using backend.Models;
using backend.Services;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Controllers;

public class SpecializationControllerTests
{
    private static (SpecializationController Controller, AdminTokenService TokenService, string Token) BuildController(
        backend.Data.ApplicationDbContext db,
        bool includeAdminHeader = true,
        bool validAdmin = true)
    {
        var tokenService = new AdminTokenService();
        var services = new ServiceCollection();
        services.AddSingleton(tokenService);
        var provider = services.BuildServiceProvider();

        var context = new DefaultHttpContext { RequestServices = provider };
        var token = string.Empty;

        if (includeAdminHeader)
        {
            token = validAdmin ? tokenService.GenerateToken(1) : "invalid";
            context.Request.Headers["Admin-Token"] = token;
        }

        var controller = new SpecializationController(db, NullLogger<SpecializationController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = context }
        };

        return (controller, tokenService, token);
    }

    [Fact]
    public async Task GetAll_ShouldReturnOnlyActiveSpecializations()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.AddRange(
            new Specialization { Name = "Info", Code = "INFO", IsActive = true },
            new Specialization { Name = "GC", Code = "GC", IsActive = false });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db, includeAdminHeader: false);

        var result = await controller.GetAll();

        result.Value.Should().HaveCount(1);
        result.Value!.Single().Code.Should().Be("INFO");
    }

    [Fact]
    public async Task GetById_ShouldReturnNotFound_WhenMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var (controller, _, _) = BuildController(db, includeAdminHeader: false);

        var result = await controller.GetById(123);

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task GetById_ShouldReturnSpecialization_WhenFound()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 42, Name = "Info", Code = "INFO", IsActive = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db, includeAdminHeader: false);
        var result = await controller.GetById(42);

        result.Value.Should().NotBeNull();
        result.Value!.Code.Should().Be("INFO");
    }

    [Fact]
    public async Task Update_ShouldReturnUnauthorized_WhenTokenInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        db.Specializations.Add(new Specialization { Id = 7, Name = "Old", Code = "OLD", IsActive = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db, includeAdminHeader: true, validAdmin: false);
        var result = await controller.Update(7, new Specialization { Name = "N", Code = "N" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task GetAllIncludingInactive_ShouldReturnBothActiveAndInactive()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.AddRange(
            new Specialization { Name = "Info", Code = "INFO", IsActive = true },
            new Specialization { Name = "GC", Code = "GC", IsActive = false });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db, includeAdminHeader: false);
        var result = await controller.GetAllIncludingInactive();

        result.Value.Should().HaveCount(2);
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenHeaderMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db, includeAdminHeader: false);

        var result = await controller.Create(new Specialization { Name = "Info", Code = "INFO" });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnUnauthorized_WhenTokenInvalid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db, includeAdminHeader: true, validAdmin: false);

        var result = await controller.Create(new Specialization { Name = "Info", Code = "INFO" });

        result.Result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnConflict_WhenCodeAlreadyExists()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        db.Specializations.Add(new Specialization { Name = "Info", Code = "INFO" });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db);

        var result = await controller.Create(new Specialization { Name = "Autre", Code = "INFO" });

        result.Result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Create_ShouldReturnCreated_WhenValid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db);

        var result = await controller.Create(new Specialization { Name = "Info", Code = "INFO" });

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        db.Specializations.Should().ContainSingle(s => s.Code == "INFO");
    }

    [Fact]
    public async Task Create_ShouldReturnBadRequest_WhenNameOrCodeMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db);
        var result = await controller.Create(new Specialization { Name = "", Code = "" });

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Update_ShouldReturnNoContent_WhenValid()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        db.Specializations.Add(new Specialization { Id = 5, Name = "Old", Code = "OLD", IsActive = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db);

        var result = await controller.Update(5, new Specialization { Name = "New", Code = "NEW", Description = "Desc", IsActive = true });

        result.Should().BeOfType<NoContentResult>();
        var spec = await db.Specializations.FindAsync(5);
        spec!.Name.Should().Be("New");
        spec.Code.Should().Be("NEW");
    }

    [Fact]
    public async Task Update_ShouldReturnUnauthorized_WhenHeaderMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        db.Specializations.Add(new Specialization { Id = 6, Name = "Old", Code = "OLD", IsActive = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db, includeAdminHeader: false);
        var result = await controller.Update(6, new Specialization { Name = "N", Code = "N" });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Update_ShouldReturnNotFound_WhenMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db);
        var result = await controller.Update(999, new Specialization { Name = "N", Code = "N" });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Update_ShouldReturnConflict_WhenCodeAlreadyExists()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        db.Specializations.AddRange(
            new Specialization { Id = 10, Name = "Info", Code = "INFO", IsActive = true },
            new Specialization { Id = 11, Name = "GC", Code = "GC", IsActive = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db);
        var result = await controller.Update(11, new Specialization { Name = "GC", Code = "INFO", IsActive = true });

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task Delete_ShouldSoftDeleteAndReturnNoContent()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        db.Specializations.Add(new Specialization { Id = 9, Name = "ToDelete", Code = "DEL", IsActive = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db);

        var result = await controller.Delete(9);

        result.Should().BeOfType<NoContentResult>();
        (await db.Specializations.FindAsync(9))!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Delete_ShouldReturnNotFound_WhenMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db);
        var result = await controller.Delete(777);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task Delete_ShouldReturnUnauthorized_WhenHeaderMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "admin", Name = "A", Firstname = "B", Email = "a@b.c", Year = "ADMIN", IsAdmin = true });
        db.Specializations.Add(new Specialization { Id = 99, Name = "X", Code = "X", IsActive = true });
        await db.SaveChangesAsync();

        var (controller, _, _) = BuildController(db, includeAdminHeader: false);
        var result = await controller.Delete(99);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }
}
