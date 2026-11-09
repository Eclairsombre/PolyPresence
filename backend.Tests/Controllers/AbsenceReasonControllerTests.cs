using System.Security.Claims;
using backend.Controllers;
using backend.Data;
using backend.Models;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Controllers;

public class AbsenceReasonControllerTests
{
    private const int AdminId = 900;
    private const int ProfId = 800;
    private const int StudentId = 1;

    private static async Task<ApplicationDbContext> SeedAsync()
    {
        var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User { Id = AdminId, StudentNumber = "ADM", Name = "Scola", Firstname = "R", Email = "s@x.fr", Year = "ADMIN", IsAdmin = true },
            new User { Id = ProfId, Name = "PROF", Firstname = "P", Email = "p@x.fr", Year = "PROF", IsProfessor = true },
            new User { Id = StudentId, StudentNumber = "p1", Name = "Etu", Firstname = "E", Email = "e@x.fr", Year = "3A" });
        db.AbsenceReasons.AddRange(
            new AbsenceReason { Id = 1, Label = "Certificat médical", SortOrder = 2 },
            new AbsenceReason { Id = 2, Label = "Grève TCL", SortOrder = 1 },
            new AbsenceReason { Id = 3, Label = "Ancien motif", SortOrder = 3, IsActive = false });
        await db.SaveChangesAsync();
        return db;
    }

    private static AbsenceReasonController Controller(ApplicationDbContext db, int userId) =>
        new(db, NullLogger<AbsenceReasonController>.Instance)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(
                        new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) }, "test"))
                }
            }
        };

    private static List<AbsenceReasonController.AbsenceReasonDto> Reasons(IActionResult result)
        => ((List<AbsenceReasonController.AbsenceReasonDto>)((OkObjectResult)result).Value!);

    [Fact]
    public async Task GetAll_ListsActiveReasonsInDisplayOrder()
    {
        await using var db = await SeedAsync();

        var reasons = Reasons(await Controller(db, ProfId).GetAll(includeInactive: true));

        reasons.Select(r => r.Label).Should().Equal("Grève TCL", "Certificat médical");
    }

    [Fact]
    public async Task GetAll_IncludeInactive_IsReservedToTheScolarite()
    {
        await using var db = await SeedAsync();

        var reasons = Reasons(await Controller(db, AdminId).GetAll(includeInactive: true));

        reasons.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetAll_StudentIsRefused()
    {
        await using var db = await SeedAsync();

        var result = await Controller(db, StudentId).GetAll();

        ((ObjectResult)result).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Create_AppendsAtTheEnd_AndRejectsDuplicates()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, AdminId);

        var created = (OkObjectResult)await controller.Create(new AbsenceReasonController.ReasonCreateModel { Label = "  Mariage  " });
        var duplicate = await controller.Create(new AbsenceReasonController.ReasonCreateModel { Label = "grève tcl" });

        var dto = (AbsenceReasonController.AbsenceReasonDto)created.Value!;
        dto.Label.Should().Be("Mariage");
        dto.SortOrder.Should().Be(4);
        duplicate.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CreateAndUpdate_AreReservedToTheScolarite()
    {
        await using var db = await SeedAsync();
        var controller = Controller(db, ProfId);

        var create = await controller.Create(new AbsenceReasonController.ReasonCreateModel { Label = "Mariage" });
        var update = await controller.Update(1, new AbsenceReasonController.ReasonUpdateModel { IsActive = false });

        ((ObjectResult)create).StatusCode.Should().Be(403);
        ((ObjectResult)update).StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task Update_RenamesAndDeactivates()
    {
        await using var db = await SeedAsync();

        await Controller(db, AdminId).Update(1, new AbsenceReasonController.ReasonUpdateModel
        {
            Label = "Certificat médical ou arrêt",
            IsActive = false
        });

        var reason = await db.AbsenceReasons.FindAsync(1);
        reason!.Label.Should().Be("Certificat médical ou arrêt");
        reason.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Update_EmptyLabel_IsRejected()
    {
        await using var db = await SeedAsync();

        var result = await Controller(db, AdminId).Update(1, new AbsenceReasonController.ReasonUpdateModel { Label = " " });

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
