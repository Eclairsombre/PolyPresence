using backend.Controllers;
using backend.Models;
using backend.Services;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace backend.Tests.Controllers;

// Note : la logique pure de parsing/normalisation/fusion est testée dans
// backend.Tests.Services.IcsImportHelperTests. Ici on couvre le contrôleur :
// endpoints et synchronisation base de données.
public class ImportControllerTests
{
    private static ImportController BuildController(backend.Data.ApplicationDbContext db)
    {
        return new ImportController(
            db,
            NullLogger<ImportController>.Instance,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    [Fact]
    public async Task ImportIcs_ShouldReturnBadRequest_WhenUrlOrYearMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.ImportIcs(new ImportController.ImportIcsModel { IcsUrl = "", Year = "" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ImportIcs_ShouldReturnBadRequest_WhenSpecializationNotFound()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.ImportIcs(new ImportController.ImportIcsModel
        {
            IcsUrl = "http://example.com/edt.ics",
            Year = "3A",
            SpecializationId = 999
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ImportIcs_ShouldReturnBadRequest_WhenNoDefaultSpecializationExists()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Name = "GC", Code = "GC" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.ImportIcs(new ImportController.ImportIcsModel
        {
            IcsUrl = "http://example.com/edt.ics",
            Year = "3A",
            SpecializationId = null
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ImportIcs_ShouldReturnServerError_WhenFetchFails()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.ImportIcs(new ImportController.ImportIcsModel
        {
            IcsUrl = "http://127.0.0.1:1/not-reachable.ics",
            Year = "3A",
            SpecializationId = 1
        });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task ImportAllIcsLinks_ShouldNotThrow_WhenNoLinks()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var act = async () => await controller.ImportAllIcsLinks(db, NullLogger<ImportController>.Instance);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SyncWithDatabase_ShouldCreateSessionAndAttendances_WhenNewSessionImported()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User { Id = 1, StudentNumber = "S1", Name = "A", Firstname = "B", Email = "a@b.fr", Year = "3A", SpecializationId = 1, IsDeleted = false },
            new User { Id = 2, StudentNumber = "S2", Name = "C", Firstname = "D", Email = "c@d.fr", Year = "3A", SpecializationId = 1, IsDeleted = true },
            new User { Id = 3, StudentNumber = "S3", Name = "E", Firstname = "F", Email = "e@f.fr", Year = "3A", SpecializationId = 2, IsDeleted = false });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var importedList = new List<ImportedSession>
        {
            new()
            {
                Date = DateTime.Today, Start = TimeSpan.FromHours(8), End = TimeSpan.FromHours(10),
                Name = "Algo", Room = "A1", ProfId = "11", ProfId2 = "", Year = "3A", TargetGroup = "3A-1"
            }
        };

        await InvokeSyncWithDatabase(controller, importedList, "3A", 1);

        var session = await db.Sessions.SingleAsync();
        session.Name.Should().Be("Algo");
        session.SpecializationId.Should().Be(1);
        session.TargetGroup.Should().Be("3A-1");

        var attendances = await db.Attendances.ToListAsync();
        attendances.Should().HaveCount(1);
        attendances.Single().StudentId.Should().Be(1); // seul étudiant 3A/spé1 non supprimé
    }

    [Fact]
    public async Task SyncWithDatabase_ShouldUpdateMatch_AndDeleteMissingSessions()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var date = DateTime.Today;
        var start = DateTime.SpecifyKind(date.AddHours(8), DateTimeKind.Unspecified);
        var end = DateTime.SpecifyKind(date.AddHours(10), DateTimeKind.Unspecified);

        db.Sessions.AddRange(
            new Session
            {
                Id = 100, Date = date, StartTime = start, EndTime = end, Year = "3A",
                Name = "Old", Room = "R1", ValidationCode = "1111", SpecializationId = 1,
                TargetGroup = "", ProfId = "1", ProfId2 = ""
            },
            new Session
            {
                Id = 101, Date = date,
                StartTime = DateTime.SpecifyKind(date.AddHours(14), DateTimeKind.Unspecified),
                EndTime = DateTime.SpecifyKind(date.AddHours(16), DateTimeKind.Unspecified),
                Year = "3A", Name = "ToDelete", Room = "R2", ValidationCode = "2222",
                SpecializationId = 1, TargetGroup = "", ProfId = "2", ProfId2 = ""
            });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var importedList = new List<ImportedSession>
        {
            new()
            {
                Date = date, Start = TimeSpan.FromHours(8), End = TimeSpan.FromHours(10),
                Name = "Updated", Room = "R9", ProfId = "9", ProfId2 = "10", Year = "3A",
                TargetGroup = "3A-2", IsMerged = true
            }
        };

        await InvokeSyncWithDatabase(controller, importedList, "3A", 1);

        var sessions = await db.Sessions.OrderBy(s => s.Id).ToListAsync();
        sessions.Should().HaveCount(1);
        sessions[0].Id.Should().Be(100);
        sessions[0].Name.Should().Be("Updated");
        sessions[0].Room.Should().Be("R9");
        sessions[0].ProfId.Should().Be("9");
        sessions[0].ProfId2.Should().Be("10");
        sessions[0].TargetGroup.Should().Be("3A-2");
        sessions[0].IsMerged.Should().BeTrue();
    }

    // SyncWithDatabase reste privé (logique interne d'écriture BDD) → invoqué par réflexion.
    private static async Task InvokeSyncWithDatabase(
        ImportController controller, List<ImportedSession> imported, string year, int specializationId)
    {
        var sync = typeof(ImportController).GetMethod("SyncWithDatabase", BindingFlags.NonPublic | BindingFlags.Instance);
        sync.Should().NotBeNull();
        var task = (Task)sync!.Invoke(controller, new object[] { imported, year, specializationId })!;
        await task;
    }
}
