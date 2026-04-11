using backend.Controllers;
using backend.Models;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Infrastructure;

namespace backend.Tests.Controllers;

public class MailPreferencesControllerTests
{
    private static MailPreferencesController BuildController(backend.Data.ApplicationDbContext db)
    {
        var serviceScopeFactory = new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
        return new MailPreferencesController(db, NullLogger<SessionController>.Instance, serviceScopeFactory);
    }

    [Fact]
    public async Task GetPdf_ShouldReturnNotFound_WhenSessionMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.GetPdf(404);

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetPreferences_ShouldReturnNotFound_WhenUserMissing()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = controller.GetPreferences("UNKNOWN");

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void GetPreferences_ShouldCreateDefaultPreferences_WhenNoneExists()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 1,
            StudentNumber = "S1",
            Name = "Doe",
            Firstname = "John",
            Email = "john@doe.fr",
            Year = "3A"
        });
        db.SaveChanges();

        var controller = BuildController(db);

        var result = controller.GetPreferences("S1");

        result.Should().BeOfType<OkObjectResult>();
        db.MailPreferences.Should().ContainSingle();
        db.Users.Single(u => u.StudentNumber == "S1").MailPreferencesId.Should().NotBeNull();
    }

    [Fact]
    public void UpdatePreferences_ShouldReturnNotFound_WhenUserMissing()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = controller.UpdatePreferences("UNKNOWN", new MailPreferences
        {
            EmailTo = "mail@test.fr",
            Days = new List<string> { "Lundi" },
            Active = true
        });

        result.Should().BeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public void UpdatePreferences_ShouldCreatePreferences_WhenUserHasNone()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 2,
            StudentNumber = "S2",
            Name = "User",
            Firstname = "Two",
            Email = "s2@test.fr",
            Year = "3A"
        });
        db.SaveChanges();

        var controller = BuildController(db);

        var result = controller.UpdatePreferences("S2", new MailPreferences
        {
            EmailTo = "prefs@test.fr",
            Days = new List<string> { "Mardi", "Jeudi" },
            Active = true
        });

        result.Should().BeOfType<NoContentResult>();
        db.Users.Single(u => u.StudentNumber == "S2").MailPreferencesId.Should().NotBeNull();
    }

    [Fact]
    public void UpdatePreferences_ShouldUpdateExistingPreferences()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var prefs = new MailPreferences { Id = 10, EmailTo = "old@test.fr", Days = new List<string> { "Lundi" }, Active = false };
        db.MailPreferences.Add(prefs);
        db.Users.Add(new User
        {
            Id = 3,
            StudentNumber = "S3",
            Name = "User",
            Firstname = "Three",
            Email = "s3@test.fr",
            Year = "3A",
            MailPreferencesId = 10
        });
        db.SaveChanges();

        var controller = BuildController(db);

        var result = controller.UpdatePreferences("S3", new MailPreferences
        {
            EmailTo = "new@test.fr",
            Days = new List<string> { "Vendredi" },
            Active = true
        });

        result.Should().BeOfType<NoContentResult>();
        var updated = db.MailPreferences.Single(mp => mp.Id == 10);
        updated.EmailTo.Should().Be("new@test.fr");
        updated.Active.Should().BeTrue();
        updated.Days.Should().ContainSingle().Which.Should().Be("Vendredi");
    }

    [Fact]
    public void TestMail_ShouldReturnBadRequest_WhenMailMissing()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = controller.TestMail(string.Empty);

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void TestMail_ShouldReturn500_WhenFromEmailMissing()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var previousFrom = Environment.GetEnvironmentVariable("SMTP_FROM_EMAIL");
        try
        {
            Environment.SetEnvironmentVariable("SMTP_FROM_EMAIL", null);

            var result = controller.TestMail("receiver@test.fr");

            result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
        }
        finally
        {
            Environment.SetEnvironmentVariable("SMTP_FROM_EMAIL", previousFrom);
        }
    }

    [Fact]
    public async Task GetPdf_ShouldReturnPdfFile_WhenSessionExists()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        QuestPDF.Settings.License = LicenseType.Community;

        var specialization = new Specialization { Id = 1, Name = "Informatique" };
        db.Specializations.Add(specialization);

        var user = new User
        {
            Id = 100,
            StudentNumber = "S100",
            Name = "Martin",
            Firstname = "Alice",
            Email = "alice.martin@test.fr",
            Year = "3A"
        };
        db.Users.Add(user);

        var session = new Session
        {
            Id = 50,
            Name = "Algo",
            Year = "3A",
            Date = DateTime.Today,
            StartTime = DateTime.Today.AddHours(8),
            EndTime = DateTime.Today.AddHours(10),
            Room = "A101",
            ValidationCode = "VAL",
            SpecializationId = 1,
            TargetGroup = "3A-1"
        };
        db.Sessions.Add(session);

        db.Attendances.Add(new Attendance
        {
            SessionId = session.Id,
            StudentId = user.Id,
            Status = AttendanceStatus.Present,
            Comment = ""
        });

        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.GetPdf(session.Id);

        var file = result.Should().BeOfType<FileContentResult>().Subject;
        file.ContentType.Should().Be("application/pdf");
        file.FileContents.Should().NotBeNull();
        file.FileContents.Length.Should().BeGreaterThan(0);
    }

    [Theory]
    [InlineData("3A")]
    [InlineData("4A")]
    [InlineData("5A")]
    public void GetPromoYears_ShouldReturnRange_ForKnownPromo(string promo)
    {
        var result = MailPreferencesController.GetPromoYears(promo);
        result.Should().NotBeNullOrEmpty();
        result.Should().Contain("-");
    }

    [Fact]
    public void GetPromoYears_ShouldReturnEmpty_ForUnknownPromo()
    {
        var result = MailPreferencesController.GetPromoYears("X");
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateAndSendZip_ShouldNotThrow_WhenNoActivePreferences()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 200,
            StudentNumber = "S200",
            Name = "No",
            Firstname = "Prefs",
            Email = "s200@test.fr",
            Year = "3A"
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var act = async () => await controller.GenerateAndSendZip();

        await act.Should().NotThrowAsync();
    }
}
