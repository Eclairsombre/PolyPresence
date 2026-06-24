using backend.Controllers;
using backend.Models;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Controllers;

public class ProfessorControllerTests
{
    private static User Prof(string name, string firstname, string email, int id = 0) => new User
    {
        Id = id,
        Name = name,
        Firstname = firstname,
        Email = email,
        Year = "PROF",
        IsProfessor = true
    };

    [Fact]
    public void GetProfessors_ShouldReturnOkList()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(Prof("Dupont", "Jean", "a@b.fr"));
        db.SaveChanges();

        var controller = new ProfessorController(db);

        var result = controller.GetProfessors();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public void GetProfessorById_ShouldReturnNotFound_WhenMissing()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = new ProfessorController(db);

        var result = controller.GetProfessorById(42);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetProfessorById_ShouldReturnOk_WhenFound()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(Prof("Found", "Prof", "found@prof.fr", id: 33));
        db.SaveChanges();

        var controller = new ProfessorController(db);

        var result = controller.GetProfessorById(33);

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task CreateProfessor_ShouldReturnBadRequest_WhenInvalidPayload()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = new ProfessorController(db);

        var result = await controller.CreateProfessor(new ProfessorController.CreateProfessorModel());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task CreateProfessor_ShouldReturnConflict_WhenDuplicate()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(Prof("Dupont", "Jean", "a@b.fr"));
        await db.SaveChangesAsync();

        var controller = new ProfessorController(db);

        var result = await controller.CreateProfessor(new ProfessorController.CreateProfessorModel
        {
            Name = "Dupont",
            Firstname = "Jean",
            Email = "x@y.fr"
        });

        result.Should().BeOfType<ConflictObjectResult>();
    }

    [Fact]
    public async Task CreateProfessor_ShouldCreateAndReturnCreatedAt()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = new ProfessorController(db);

        var result = await controller.CreateProfessor(new ProfessorController.CreateProfessorModel
        {
            Name = "Durand",
            Firstname = "Alice",
            Email = "alice@poly.fr"
        });

        result.Should().BeOfType<CreatedAtActionResult>();
        db.Users.Should().ContainSingle(u => u.IsProfessor && u.Name == "Durand" && u.Firstname == "Alice");
    }

    [Fact]
    public async Task UpdateProfessorEmail_ShouldValidateInput()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = new ProfessorController(db);

        var missing = await controller.UpdateProfessorEmail(1, new ProfessorController.UpdateEmailModel { Email = "" });
        missing.Should().BeOfType<NotFoundObjectResult>();

        db.Users.Add(Prof("P", "Q", "old@p.fr", id: 2));
        await db.SaveChangesAsync();

        var invalid = await controller.UpdateProfessorEmail(2, new ProfessorController.UpdateEmailModel { Email = "" });
        invalid.Should().BeOfType<BadRequestObjectResult>();

        var ok = await controller.UpdateProfessorEmail(2, new ProfessorController.UpdateEmailModel { Email = "new@p.fr" });
        ok.Should().BeOfType<OkObjectResult>();
        (await db.Users.FindAsync(2))!.Email.Should().Be("new@p.fr");
    }

    [Fact]
    public async Task FindOrCreateProfessor_ShouldReturnBadRequest_WhenNameMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = new ProfessorController(db);

        var result = await controller.FindOrCreateProfessor(new ProfessorController.CreateProfessorModel());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task FindOrCreateProfessor_ShouldReturnExistingOrCreate()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(Prof("Same", "Prof", "s@p.fr", id: 7));
        await db.SaveChangesAsync();

        var controller = new ProfessorController(db);

        var existing = await controller.FindOrCreateProfessor(new ProfessorController.CreateProfessorModel { Name = "Same", Firstname = "Prof" });
        existing.Should().BeOfType<OkObjectResult>();

        var created = await controller.FindOrCreateProfessor(new ProfessorController.CreateProfessorModel { Name = "New", Firstname = "One", Email = "n@o.fr" });
        created.Should().BeOfType<OkObjectResult>();
        db.Users.Should().Contain(u => u.IsProfessor && u.Name == "New" && u.Firstname == "One");
    }

    [Fact]
    public async Task DeleteProfessor_ShouldDeleteAndUnlinkSessions()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(Prof("To", "Delete", "t@d.fr", id: 3));
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        db.Sessions.Add(new Session
        {
            Id = 11,
            Name = "Cours",
            Year = "3A",
            Room = "B10",
            Date = DateTime.UtcNow.Date,
            StartTime = DateTime.UtcNow,
            EndTime = DateTime.UtcNow.AddHours(2),
            ValidationCode = "1234",
            ProfId = "3",
            ProfId2 = "3",
            SpecializationId = 1
        });
        await db.SaveChangesAsync();

        var controller = new ProfessorController(db);

        var result = await controller.DeleteProfessor(3);

        result.Should().BeOfType<OkObjectResult>();
        db.Users.Where(u => u.IsProfessor).Should().BeEmpty();
        var session = await db.Sessions.FirstAsync();
        session.ProfId.Should().BeNull();
        session.ProfId2.Should().BeNull();
    }

    [Fact]
    public async Task DeleteProfessor_ShouldReturnNotFound_WhenMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = new ProfessorController(db);

        var result = await controller.DeleteProfessor(999);

        result.Should().BeOfType<NotFoundObjectResult>();
    }
}
