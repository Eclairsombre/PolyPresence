using System.Security.Claims;
using System.Text;
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

/// <summary>
/// Analyse préalable d'un lien ICS : ce qui permet à l'administrateur de choisir le
/// libellé promo dans une liste au lieu de le retaper au caractère près, et de voir
/// qu'un calendrier est vide au lieu d'attendre un import qui ne produira rien.
/// </summary>
public class PreviewIcsTests
{
    private static string FixturePath(string name)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static ImportController BuildController(
        backend.Data.ApplicationDbContext db, ClaimsPrincipal? principal = null)
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();

        var controller = new ImportController(
            db,
            NullLogger<ImportController>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IHttpClientFactory>());

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = principal ?? new ClaimsPrincipal(new ClaimsIdentity())
            }
        };
        return controller;
    }

    private static async Task<(backend.Data.ApplicationDbContext Db, ClaimsPrincipal Admin)> SeedAsync()
    {
        var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 900, StudentNumber = "ADM", Name = "Ad", Firstname = "Min",
            Email = "adm@x.fr", Year = "ADMIN", IsAdmin = true, Signature = ""
        });
        await db.SaveChangesAsync();

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "900") }, "test"));
        return (db, principal);
    }

    [Fact]
    public async Task PreviewIcs_RequiresAuthentication()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.PreviewIcs(new ImportController.PreviewIcsModel
        {
            IcsUrl = "https://edt.univ-lyon1.fr/x.ics"
        });

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task PreviewIcs_IsForbiddenForAPlainStudent()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.Add(new User
        {
            Id = 5, StudentNumber = "p5", Name = "N", Firstname = "P",
            Email = "p5@x.fr", Year = "3A", Signature = ""
        });
        await db.SaveChangesAsync();

        var controller = BuildController(db, new ClaimsPrincipal(new ClaimsIdentity(
            new[] { new Claim(ClaimTypes.NameIdentifier, "5") }, "test")));

        var result = await controller.PreviewIcs(new ImportController.PreviewIcsModel
        {
            IcsUrl = "https://edt.univ-lyon1.fr/x.ics"
        });

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task PreviewIcs_RejectsAnEmptyUrl()
    {
        var (db, admin) = await SeedAsync();
        await using var _ = db;

        var result = await BuildController(db, admin)
            .PreviewIcs(new ImportController.PreviewIcsModel { IcsUrl = "" });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task PreviewIcs_AppliesTheSameSsrfGuardAsTheImport()
    {
        var (db, admin) = await SeedAsync();
        await using var _ = db;

        // L'analyse télécharge une URL fournie par l'utilisateur : elle doit être
        // protégée exactement comme l'import.
        var result = await BuildController(db, admin)
            .PreviewIcs(new ImportController.PreviewIcsModel
            {
                IcsUrl = "http://127.0.0.1:1/not-reachable.ics"
            });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    // ---------- L'heuristique sur laquelle repose la suggestion ----------

    /// <summary>Construit un VEVENT minimal ciblant les libellés donnés.</summary>
    private static string Event(int index, params string[] labels)
    {
        // Separateur ADE : un backslash-n LITTERAL dans le fichier, pas un saut de ligne.
        var description = "\\n" + string.Join("\\n", labels) + "\\n";
        return $"BEGIN:VEVENT\r\nUID:u{index}\r\n" +
               $"DTSTART:2026092{index % 10}T080000Z\r\nDTEND:2026092{index % 10}T100000Z\r\n" +
               "SUMMARY:Cours\r\nLOCATION:R1\r\n" +
               $"DESCRIPTION:{description}\r\nEND:VEVENT\r\n";
    }

    private static string Calendar(IEnumerable<string> events) =>
        "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//test//\r\n" +
        string.Concat(events) + "END:VCALENDAR\r\n";

    [Fact]
    public void TheSuggestedLabelIsTheMostFrequentOne()
    {
        // Le champ pré-sélectionné dans l'écran est le libellé le plus fréquent du
        // calendrier. Ce classement est reproduit ici sur des proportions fidèles aux
        // exports réels — INFO 3A : 185 séances de promo sur 298, chaque sous-groupe à 61 ;
        // MECA 3A : 165 sur 420 — et non sur la fixture, dont les proportions sont
        // volontairement différentes puisqu'elle couvre les formes de ciblage.
        const string promo = "Diplôme d'Ingénieur POLYTECH 3A (Informatique)";

        var events = new List<string>();
        for (var i = 0; i < 12; i++) events.Add(Event(i, promo));
        for (var i = 12; i < 16; i++) events.Add(Event(i, "INFO 1-A", "INFO 1-B"));

        var ranking = IcsImportHelper.ParseCalendar(Calendar(events))
            .SelectMany(e => e.GroupLabels)
            .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        ranking[0].Label.Should().Be(promo);
        ranking[0].Count.Should().Be(12);
        ranking[1].Count.Should().Be(4);
    }

    [Fact]
    public void EveryLabelOfThePromoFixtureIsRanked()
    {
        // Sur la fixture, on vérifie seulement que le classement est complet et correct :
        // 16 sous-groupes, le libellé promo, et les 4 libellés des autres filières
        // apportés par le cours mutualisé.
        var content = File.ReadAllText(FixturePath("edt_info3a.ics"), Encoding.UTF8);

        var ranking = IcsImportHelper.ParseCalendar(content)
            .SelectMany(e => e.GroupLabels)
            .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
            .Select(g => new { Label = g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count)
            .ToList();

        ranking.Should().HaveCount(21);
        ranking.Select(x => x.Count).Should().BeInDescendingOrder();
        ranking.Should().Contain(x => x.Label == "Diplôme d'Ingénieur POLYTECH 3A (Informatique)");
    }

    [Fact]
    public void ALanguageCalendarExposesItsGroupCodes_AndNoPromoLabel()
    {
        // Sur un calendrier de langues, le libellé le plus fréquent est un code de
        // groupe : c'est pourquoi l'écran masque le champ « libellé promo » dans ce cas.
        var content = File.ReadAllText(FixturePath("lv2.ics"), Encoding.UTF8);

        var labels = IcsImportHelper.ParseCalendar(content)
            .SelectMany(e => e.GroupLabels)
            .Distinct()
            .ToList();

        labels.Should().BeEquivalentTo(new[] { "A-PL9004TR-BE91", "B-PL9004TR-BE92" });
    }

    [Fact]
    public void AnEmptyCalendarYieldsNoEventAndNoLabel()
    {
        // Le cas réel des emplois du temps de langues non publiés : ADE renvoie un
        // VCALENDAR valide mais vide. L'écran doit pouvoir le dire explicitement.
        const string empty =
            "BEGIN:VCALENDAR\r\nMETHOD:REQUEST\r\nPRODID:-//ADE/version 6.0\r\n" +
            "VERSION:2.0\r\nCALSCALE:GREGORIAN\r\nEND:VCALENDAR\r\n";

        var events = IcsImportHelper.ParseCalendar(empty);

        events.Should().BeEmpty();
    }
}
