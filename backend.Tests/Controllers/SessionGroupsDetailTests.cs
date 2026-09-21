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
/// Groupes visés exposés par le détail d'une séance. C'est ce qui explique la
/// composition de la feuille d'émargement : sans cette information, rien ne distingue
/// deux TP donnés au même créneau à des publics différents.
/// </summary>
public class SessionGroupsDetailTests
{
    private static SessionController BuildController(
        backend.Data.ApplicationDbContext db, int? userId = null)
    {
        var controller = new SessionController(
            db,
            NullLogger<SessionController>.Instance,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());

        var principal = userId.HasValue
            ? new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString()) }, "test"))
            : new ClaimsPrincipal(new ClaimsIdentity());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = principal }
        };
        return controller;
    }

    /// <summary>Lit la propriété "Groups" de la projection anonyme renvoyée par l'API.</summary>
    private static List<(int Id, string Label, string DisplayName, GroupType Type)> ReadGroups(object? payload)
    {
        var property = payload!.GetType().GetProperty("Groups");
        property.Should().NotBeNull("le détail d'une séance doit exposer ses groupes");

        var items = (System.Collections.IEnumerable)property!.GetValue(payload)!;
        var result = new List<(int, string, string, GroupType)>();

        foreach (var item in items)
        {
            var type = item.GetType();
            result.Add((
                (int)type.GetProperty("Id")!.GetValue(item)!,
                (string)type.GetProperty("Label")!.GetValue(item)!,
                (string)type.GetProperty("DisplayName")!.GetValue(item)!,
                (GroupType)type.GetProperty("Type")!.GetValue(item)!));
        }
        return result;
    }

    private static async Task<backend.Data.ApplicationDbContext> SeedAsync()
    {
        var db = DbContextHelper.CreateInMemoryDbContext();

        db.Specializations.Add(new Specialization { Id = 1, Name = "Informatique", Code = "INFO" });
        db.Users.AddRange(
            new User { Id = 900, StudentNumber = "ADM", Name = "Ad", Firstname = "Min", Email = "adm@x.fr", Year = "ADMIN", IsAdmin = true, Signature = "" },
            new User { Id = 5, StudentNumber = "p5", Name = "Etu", Firstname = "Diant", Email = "p5@x.fr", Year = "3A", SpecializationId = 1, Signature = "" });

        db.Groups.AddRange(
            new Group { Id = 1, Label = "Diplôme d'Ingénieur POLYTECH 3A (Informatique)", DisplayName = "INFO 3A", Type = GroupType.Promo, SpecializationId = 1, Year = "3A" },
            new Group { Id = 10, Label = "INFO 1-B", DisplayName = "INFO 1-B", Type = GroupType.Sub, SpecializationId = 1, Year = "3A" },
            new Group { Id = 11, Label = "INFO 1-A", DisplayName = "INFO 1-A", Type = GroupType.Sub, SpecializationId = 1, Year = "3A" },
            new Group { Id = 20, Label = "A-PL9003TR-BE91", DisplayName = "Anglais gr. A", Type = GroupType.Lv1, SpecializationId = 1, Year = "3A" });

        Session S(int id, string name, params int[] groupIds) => new()
        {
            Id = id, Date = DateTime.Today,
            StartTime = DateTime.Today.AddHours(9), EndTime = DateTime.Today.AddHours(11),
            Year = "3A", Name = name, Room = "R" + id, ValidationCode = "1234", SpecializationId = 1,
            SessionGroups = groupIds.Select(g => new SessionGroup { SessionId = id, GroupId = g }).ToList()
        };

        db.Sessions.AddRange(
            S(100, "TP", 10, 11, 20),
            S(101, "CM", 1),
            S(102, "Séance manuelle"));

        await db.SaveChangesAsync();
        return db;
    }

    [Fact]
    public async Task GetSession_ExposesTargetGroups_SortedByTypeThenLabel()
    {
        await using var db = await SeedAsync();

        var result = await BuildController(db, 900).GetSession(100);
        var groups = ReadGroups(result.Value);

        // Tri stable : sous-groupes d'abord (Type 0), par libellé, puis les langues.
        groups.Select(g => g.Label).Should().Equal("INFO 1-A", "INFO 1-B", "A-PL9003TR-BE91");
        groups.Select(g => g.Type).Should().Equal(GroupType.Sub, GroupType.Sub, GroupType.Lv1);
        groups.Single(g => g.Type == GroupType.Lv1).DisplayName.Should().Be("Anglais gr. A");
    }

    [Fact]
    public async Task GetSession_ExposesThePromoLabelAsItsOwnType()
    {
        await using var db = await SeedAsync();

        var result = await BuildController(db, 900).GetSession(101);
        var groups = ReadGroups(result.Value);

        // L'écran l'affiche à part : ce libellé ne désigne pas un groupe d'étudiants
        // mais toute la promotion.
        groups.Should().ContainSingle().Which.Type.Should().Be(GroupType.Promo);
    }

    [Fact]
    public async Task GetSession_WithoutAnyGroup_ReturnsAnEmptyList()
    {
        // Séance créée à la main, ou importée avant l'introduction des groupes :
        // la feuille couvre alors toute la promotion.
        await using var db = await SeedAsync();

        var result = await BuildController(db, 900).GetSession(102);

        ReadGroups(result.Value).Should().BeEmpty();
    }

    [Fact]
    public async Task GetSession_ExposesGroupsToNonAdminsToo_WithoutLeakingTheValidationCode()
    {
        await using var db = await SeedAsync();

        var result = await BuildController(db, 5).GetSession(100);

        ReadGroups(result.Value).Should().HaveCount(3);
        // La projection non-admin ne doit toujours pas contenir le code de validation.
        result.Value!.GetType().GetProperty("ValidationCode").Should().BeNull();
    }
}
