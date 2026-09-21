using System.Security.Claims;
using backend.Controllers;
using backend.Models;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Controllers;

/// <summary>
/// "Cours en cours" quand un étudiant est inscrit à deux séances sur le même créneau.
///
/// Le cas se produit dès qu'un EDT de promo et un EDT de langues se chevauchent : les deux
/// calendriers sont publiés indépendamment dans ADE et rien n'y garantit qu'ils s'évitent.
/// La règle retenue est : la séance du sous-groupe prime, la langue ne sert que de repli.
/// </summary>
public class CurrentSessionLanguagePriorityTests
{
    private const int Student = 1;
    private const int SubGroupId = 10;
    private const int Lv1GroupId = 20;

    private static SessionController BuildController(backend.Data.ApplicationDbContext db, int userId)
    {
        var controller = new SessionController(
            db,
            NullLogger<SessionController>.Instance,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        // Le claim "isDelegate" fait renvoyer l'entité Session complète plutôt que la
        // projection anonyme : plus simple à vérifier, et sans effet sur le choix de séance.
        var principal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim("isDelegate", "true")
        }, "test"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    /// <summary>
    /// Prépare un étudiant inscrit à une séance de sous-groupe et/ou à une séance de langue,
    /// toutes deux en cours à l'instant présent.
    /// </summary>
    private static async Task<backend.Data.ApplicationDbContext> SeedAsync(
        bool withSubGroupSession, bool withLanguageSession)
    {
        var db = DbContextHelper.CreateInMemoryDbContext();
        var now = DateTime.Now;
        var today = now.Date;

        db.Specializations.AddRange(
            new Specialization { Id = 1, Name = "Informatique", Code = "INFO" },
            new Specialization { Id = 2, Name = "Langues", Code = "LANGUES" });

        db.Groups.AddRange(
            new Group { Id = SubGroupId, Label = "INFO 1-A", DisplayName = "INFO 1-A", Type = GroupType.Sub, SpecializationId = 1, Year = "3A" },
            new Group { Id = Lv1GroupId, Label = "A-PL9003TR-BE91", DisplayName = "Anglais A", Type = GroupType.Lv1, SpecializationId = 2, Year = "3A" });

        db.Users.Add(new User
        {
            Id = Student, StudentNumber = "p1", Name = "Alice", Firstname = "A",
            Email = "alice@x.fr", Year = "3A", SpecializationId = 1,
            SubGroupId = SubGroupId, Lv1GroupId = Lv1GroupId
        });

        Session Build(int id, string name, int specializationId, GroupType kind, int groupId) => new()
        {
            Id = id,
            Date = today,
            StartTime = now.AddHours(-1),
            EndTime = now.AddHours(1),
            Year = "3A",
            Name = name,
            Room = "R" + id,
            ValidationCode = "1234",
            SpecializationId = specializationId,
            IcsKind = kind,
            SessionGroups = new List<SessionGroup> { new() { SessionId = id, GroupId = groupId } }
        };

        if (withSubGroupSession)
            db.Sessions.Add(Build(100, "Fondts math.", 1, GroupType.Sub, SubGroupId));
        if (withLanguageSession)
            db.Sessions.Add(Build(200, "S9 - Anglais Gr A", 2, GroupType.Lv1, Lv1GroupId));

        await db.SaveChangesAsync();

        foreach (var session in db.Sessions.ToList())
            db.Attendances.Add(new Attendance { SessionId = session.Id, StudentId = Student, Status = AttendanceStatus.Absent });

        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task WhenBothOverlap_TheSubGroupSessionWins()
    {
        await using var db = await SeedAsync(withSubGroupSession: true, withLanguageSession: true);

        var result = await BuildController(db, Student).GetCurrentSession("3A");

        result.Value.Should().BeOfType<Session>()
            .Which.Id.Should().Be(100);
    }

    [Fact]
    public async Task WhenOnlyTheLanguageSessionIsRunning_ItIsReturned()
    {
        await using var db = await SeedAsync(withSubGroupSession: false, withLanguageSession: true);

        var result = await BuildController(db, Student).GetCurrentSession("3A");

        result.Value.Should().BeOfType<Session>()
            .Which.Id.Should().Be(200);
    }

    [Fact]
    public async Task WhenNothingIsRunning_ReturnsNotFound()
    {
        await using var db = await SeedAsync(withSubGroupSession: false, withLanguageSession: false);

        var result = await BuildController(db, Student).GetCurrentSession("3A");

        result.Result.Should().BeOfType<NotFoundObjectResult>();
    }
}
