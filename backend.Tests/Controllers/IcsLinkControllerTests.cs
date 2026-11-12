using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using backend.Controllers;
using backend.Models;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Controllers;

public class IcsLinkControllerTests
{
    private static IcsLinkController Build(backend.Data.ApplicationDbContext db, int userId) => new(db)
    {
        ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
            }
        }
    };

    private static IcsLinkController AsAdmin(backend.Data.ApplicationDbContext db)
    {
        if (!db.Users.Any(u => u.Id == 9000))
        {
            db.Users.Add(new User { Id = 9000, StudentNumber = "ADM9000", Name = "Ad", Firstname = "Min", Email = "adm9000@x.fr", Year = "ADMIN", IsAdmin = true });
            db.SaveChanges();
        }
        return Build(db, 9000);
    }

    [Fact]
    public async Task GetAll_ShouldReturnMappedList()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var spec = new Specialization { Name = "Informatique", Code = "INFO" };
        db.Specializations.Add(spec);
        await db.SaveChangesAsync();

        db.IcsLinks.Add(new IcsLink { Year = "3A", Url = "http://ics", SpecializationId = spec.Id });
        await db.SaveChangesAsync();

        var controller = AsAdmin(db);

        var result = await controller.GetAll();

        result.Value.Should().NotBeNull();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_WithSpecialization_ShouldPersist()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var spec = new Specialization { Name = "Informatique", Code = "INFO" };
        db.Specializations.Add(spec);
        await db.SaveChangesAsync();

        var controller = AsAdmin(db);
        var link = new IcsLink { Year = "4A", Url = "http://ics2", SpecializationId = spec.Id };

        var result = await controller.Create(link);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        db.IcsLinks.Should().HaveCount(1);
    }

    [Fact]
    public async Task Create_ShouldUseDefaultInfoSpecialization_WhenSpecializationIdIsZero()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var info = new Specialization { Id = 12, Name = "Informatique", Code = "INFO" };
        db.Specializations.Add(info);
        await db.SaveChangesAsync();

        var controller = AsAdmin(db);
        var link = new IcsLink { Year = "3A", Url = "http://ics-default", SpecializationId = 0 };

        var result = await controller.Create(link);

        result.Result.Should().BeOfType<CreatedAtActionResult>();
        db.IcsLinks.Should().ContainSingle();
        db.IcsLinks.Single().SpecializationId.Should().Be(12);
    }

    [Fact]
    public async Task Update_WithMismatchedId_ShouldReturnBadRequest()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = AsAdmin(db);

        var result = await controller.Update(1, new IcsLink { Id = 2, Year = "3A", Url = "u" });

        result.Should().BeOfType<BadRequestResult>();
    }

    [Fact]
    public async Task Update_WhenNotFound_ShouldReturnNotFound()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = AsAdmin(db);

        var result = await controller.Update(1, new IcsLink { Id = 1, Year = "3A", Url = "u" });

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Update_WhenFound_ShouldUpdate()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var spec = new Specialization { Name = "Informatique", Code = "INFO" };
        db.Specializations.Add(spec);
        db.IcsLinks.Add(new IcsLink { Id = 1, Year = "3A", Url = "old", SpecializationId = spec.Id });
        await db.SaveChangesAsync();

        var controller = AsAdmin(db);

        var result = await controller.Update(1, new IcsLink { Id = 1, Year = "5A", Url = "new", SpecializationId = spec.Id });

        result.Should().BeOfType<NoContentResult>();
        var updated = await db.IcsLinks.FirstAsync();
        updated.Year.Should().Be("5A");
        updated.Url.Should().Be("new");
    }

    [Fact]
    public async Task Update_ShouldKeepExistingSpecialization_WhenIncomingSpecializationIsZero()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var spec = new Specialization { Id = 3, Name = "Informatique", Code = "INFO" };
        db.Specializations.Add(spec);
        db.IcsLinks.Add(new IcsLink { Id = 2, Year = "3A", Url = "old", SpecializationId = 3 });
        await db.SaveChangesAsync();

        var controller = AsAdmin(db);

        var result = await controller.Update(2, new IcsLink { Id = 2, Year = "4A", Url = "new", SpecializationId = 0 });

        result.Should().BeOfType<NoContentResult>();
        var updated = await db.IcsLinks.FirstAsync(l => l.Id == 2);
        updated.SpecializationId.Should().Be(3);
    }

    [Fact]
    public async Task Delete_ShouldReturnNotFound_OrNoContent()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = AsAdmin(db);

        var notFound = await controller.Delete(99);
        notFound.Should().BeOfType<NotFoundResult>();

        var spec = new Specialization { Name = "Informatique", Code = "INFO" };
        db.Specializations.Add(spec);
        db.IcsLinks.Add(new IcsLink { Id = 10, Year = "3A", Url = "u", SpecializationId = spec.Id });
        await db.SaveChangesAsync();

        var deleted = await controller.Delete(10);
        deleted.Should().BeOfType<NoContentResult>();
        db.IcsLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task AllEndpoints_ShouldForbidStudent()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User { Id = 1, StudentNumber = "S1", Name = "A", Firstname = "B", Email = "s1@x.fr", Year = "3A" });
        db.IcsLinks.Add(new IcsLink { Id = 1, Year = "3A", Url = "https://edt.example/real.ics" });
        await db.SaveChangesAsync();
        var controller = Build(db, 1);

        (await controller.GetAll()).Result.Should().BeOfType<ForbidResult>();
        (await controller.Create(new IcsLink { Year = "3A", Url = "https://evil.example/fake.ics" })).Result.Should().BeOfType<ForbidResult>();
        (await controller.Update(1, new IcsLink { Id = 1, Year = "3A", Url = "https://evil.example/fake.ics" })).Should().BeOfType<ForbidResult>();
        (await controller.Delete(1)).Should().BeOfType<ForbidResult>();

        (await db.IcsLinks.FindAsync(1))!.Url.Should().Be("https://edt.example/real.ics");
    }
}
