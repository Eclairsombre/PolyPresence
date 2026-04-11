using backend.Controllers;
using backend.Models;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Tests.Controllers;

public class ProfessorControllerTests
{
    [Fact]
    public void GetProfessors_ShouldReturnOkList()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Professors.Add(new Professor { Name = "Dupont", Firstname = "Jean", Email = "a@b.fr" });
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
        db.Professors.Add(new Professor { Id = 33, Name = "Found", Firstname = "Prof", Email = "found@prof.fr" });
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
        db.Professors.Add(new Professor { Name = "Dupont", Firstname = "Jean", Email = "a@b.fr" });
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
        db.Professors.Should().ContainSingle(p => p.Name == "Durand" && p.Firstname == "Alice");
    }

    [Fact]
    public async Task UpdateProfessorEmail_ShouldValidateInput()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = new ProfessorController(db);

        var missing = await controller.UpdateProfessorEmail(1, new ProfessorController.UpdateEmailModel { Email = "" });
        missing.Should().BeOfType<NotFoundObjectResult>();

        db.Professors.Add(new Professor { Id = 2, Name = "P", Firstname = "Q", Email = "old@p.fr" });
        await db.SaveChangesAsync();

        var invalid = await controller.UpdateProfessorEmail(2, new ProfessorController.UpdateEmailModel { Email = "" });
        invalid.Should().BeOfType<BadRequestObjectResult>();

        var ok = await controller.UpdateProfessorEmail(2, new ProfessorController.UpdateEmailModel { Email = "new@p.fr" });
        ok.Should().BeOfType<OkObjectResult>();
        (await db.Professors.FindAsync(2))!.Email.Should().Be("new@p.fr");
    }

    [Fact]
    public async Task FindOrCreateProfessor_ShouldReturnBadRequest_WhenNameMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = new ProfessorController(db);

        var result = await controller.FindOrCreateProfessor(new Professor());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task FindOrCreateProfessor_ShouldReturnExistingOrCreate()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Professors.Add(new Professor { Id = 7, Name = "Same", Firstname = "Prof", Email = "s@p.fr" });
        await db.SaveChangesAsync();

        var controller = new ProfessorController(db);

        var existing = await controller.FindOrCreateProfessor(new Professor { Name = "Same", Firstname = "Prof" });
        existing.Should().BeOfType<OkObjectResult>();

        var created = await controller.FindOrCreateProfessor(new Professor { Name = "New", Firstname = "One", Email = "n@o.fr" });
        created.Should().BeOfType<OkObjectResult>();
        db.Professors.Should().Contain(p => p.Name == "New" && p.Firstname == "One");
    }

    [Fact]
    public async Task DeleteProfessor_ShouldDeleteAndUnlinkSessions()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Professors.Add(new Professor { Id = 3, Name = "To", Firstname = "Delete", Email = "t@d.fr" });
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
        db.Professors.Should().BeEmpty();
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
