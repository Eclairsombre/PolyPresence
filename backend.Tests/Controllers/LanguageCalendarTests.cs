using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using backend.Controllers;
using backend.Models;
using backend.Services;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace backend.Tests.Controllers;

/// <summary>
/// Emplois du temps de langues, à partir de deux échantillons :
///
/// <b>lv1_reel_3a.ics</b> — export ADE réel de la LV1 3A (anglais), récupéré le 16/09/2026 :
/// 146 cours, 10 groupes <c>A-I3002TR-AR5x</c> (2 cours/semaine) et <c>AN5x</c> (1/semaine).
///
/// <b>lv2_mock_3a.ics</b> — LV2 fictive calquée sur cet export : même structure de description
/// (sans recopie du SUMMARY), même période, même trou de Toussaint, même changement d'heure,
/// un événement multi-groupes et un cours publié en 2×1h30. Codes, profs et créneaux inventés.
///
/// <b>Attention à la forme réelle</b> : ADE ne publie PAS deux calendriers. Il en publie un
/// seul, où les deux familles de codes cohabitent — c'est ce que vérifient les tests de la
/// section « calendrier unique », qui recollent les deux échantillons. Les tests par
/// échantillon restent utiles pour isoler une régression de parsing sur une moitié.
/// </summary>
public class LanguageCalendarTests
{
    private const int LanguesSpecId = 2;
    private const string RealLv1 = "lv1_reel_3a.ics";
    private const string MockLv2 = "lv2_mock_3a.ics";

    // ------------------------------------------------------------------ outillage

    private static string Read(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", name), Encoding.UTF8);

    private static List<ParsedIcsEvent> ParseText(string ics) => IcsImportHelper.ParseCalendar(ics);

    private static List<ParsedIcsEvent> Parse(string name) => ParseText(Read(name));

    private static List<ImportedSession> LoadText(string ics) =>
        IcsImportHelper.ApplyBusinessRules(ParseText(ics)
            .Select(ev => new ImportedSession
            {
                Date = ev.Date,
                Start = ev.Start,
                End = ev.End,
                Name = ev.Name,
                Room = ev.Room,
                // Le nom du prof sert d'identité : sans lui, deux groupes parallèles d'un
                // même cours ne différeraient que par la salle.
                ProfId = ev.Professors.Count > 0 ? ev.Professors[0].Name : "",
                ProfId2 = "",
                Year = "3A",
                Uid = ev.Uid,
                GroupLabels = ev.GroupLabels,
            })
            .ToList());

    private static List<ImportedSession> Load(string name) => LoadText(Read(name));

    /// <summary>
    /// Recolle les deux échantillons en UN calendrier : c'est la forme réellement publiée
    /// par ADE — un seul export où toutes les langues cohabitent.
    /// </summary>
    private static string SingleLanguageCalendar()
    {
        var host = Read(RealLv1);
        var guest = Read(MockLv2);

        var start = guest.IndexOf("BEGIN:VEVENT", StringComparison.Ordinal);
        var end = guest.LastIndexOf("END:VCALENDAR", StringComparison.Ordinal);
        var events = guest[start..end];

        var insertAt = host.LastIndexOf("END:VCALENDAR", StringComparison.Ordinal);
        return host[..insertAt] + events + host[insertAt..];
    }

    private static async Task SyncAsync(
        backend.Data.ApplicationDbContext db, List<ImportedSession> sessions, GroupType kind)
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();

        var controller = new ImportController(
            db,
            NullLogger<ImportController>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IHttpClientFactory>());

        var sync = typeof(ImportController).GetMethod("SyncWithDatabase", BindingFlags.NonPublic | BindingFlags.Instance);
        await (Task)sync!.Invoke(controller, new object?[] { sessions, "3A", LanguesSpecId, kind, null })!;
    }

    private static Task SyncAsync(backend.Data.ApplicationDbContext db, string fixture, GroupType kind) =>
        SyncAsync(db, Load(fixture), kind);

    private static async Task<backend.Data.ApplicationDbContext> CreateDbAsync()
    {
        var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.AddRange(
            new Specialization { Id = 1, Name = "Informatique", Code = "INFO" },
            new Specialization { Id = LanguesSpecId, Name = "Langues", Code = "LANGUES" });
        await db.SaveChangesAsync();
        return db;
    }

    /// <summary>
    /// Déclare un groupe de langue avant l'import, comme le fait l'admin depuis l'écran
    /// Groupes : c'est ce qui permet d'affecter les étudiants AVANT que l'import ne crée
    /// les séances, et donc que chaque nouvelle séance reçoive ses émargements.
    /// </summary>
    private static async Task<int> DeclareGroupAsync(
        backend.Data.ApplicationDbContext db, string label, GroupType type)
    {
        var group = new backend.Models.Group
        {
            Label = label,
            DisplayName = label,
            Type = type,
            SpecializationId = LanguesSpecId,
            Year = "3A",
        };
        db.Groups.Add(group);
        await db.SaveChangesAsync();
        return group.Id;
    }

    private static async Task<int> SessionCountForGroupAsync(backend.Data.ApplicationDbContext db, string label)
    {
        var group = await db.Groups.SingleAsync(g => g.Label == label);
        return await db.SessionGroups.CountAsync(sg => sg.GroupId == group.Id);
    }

    // ================================================================== LV1 réelle

    [Fact]
    public void RealLv1_EveryEventCarriesExactlyItsGroupCodes()
    {
        var events = Parse(RealLv1);

        events.Should().HaveCount(146);
        events.Should().OnlyContain(e => e.GroupLabels.Count >= 1);

        // Seuls de vrais codes de groupe remontent : ni l'identifiant ADE, ni le prof, ni
        // la ligne d'export. Cette LV1 ne recopie pas le SUMMARY dans la description,
        // contrairement au premier échantillon LV2 : les deux formes doivent passer.
        var codePattern = new Regex(@"^A-I3002TR-A[RN]5[1-5]$");
        events.SelectMany(e => e.GroupLabels).Should().OnlyContain(l => codePattern.IsMatch(l));
        events.SelectMany(e => e.GroupLabels).Distinct().Should().HaveCount(10);

        events.Should().OnlyContain(e => e.Professors.Count == 1);
    }

    [Fact]
    public void RealLv1_LocalTimesSurviveTheSwitchToWinterTime()
    {
        // Le même cours de 14h figure à 12:00Z en septembre et à 13:00Z en novembre.
        // Une conversion de fuseau ratée décalerait tous les cours d'une heure après le 25/10.
        var ar54 = Parse(RealLv1)
            .Where(e => e.GroupLabels.Contains("A-I3002TR-AR54") && e.Name.Contains("Lun"))
            .ToList();

        ar54.Single(e => e.Date == new DateTime(2026, 9, 7)).Start.Should().Be(new TimeSpan(14, 0, 0));
        ar54.Single(e => e.Date == new DateTime(2026, 11, 2)).Start.Should().Be(new TimeSpan(14, 0, 0));
    }

    [Fact]
    public void RealLv1_ACourseSplitInTwoIsRejoined()
    {
        // Le 21/09, AN51 est publié en 9h45–11h15 puis 11h30–13h : un seul cours.
        var sessions = Load(RealLv1);

        sessions.Should().HaveCount(145);

        var an51 = sessions
            .Where(s => s.Date == new DateTime(2026, 9, 21) && s.GroupLabels.Contains("A-I3002TR-AN51"))
            .ToList();

        an51.Should().ContainSingle();
        an51[0].Start.Should().Be(new TimeSpan(9, 45, 0));
        an51[0].End.Should().Be(new TimeSpan(13, 0, 0));
        an51[0].IsMerged.Should().BeTrue();
    }

    [Fact]
    public void RealLv1_ParallelGroupsOnTheSameSlot_StayDistinctSessions()
    {
        // Vendredi 11/09 à 9h45 : 4 groupes d'anglais renforcé, 4 profs, 4 salles.
        // Avant les groupes, ces cours étaient recombinés en une seule séance à 2 profs.
        var friday = Load(RealLv1)
            .Where(s => s.Date == new DateTime(2026, 9, 11) && s.Start == new TimeSpan(9, 45, 0))
            .ToList();

        friday.Should().HaveCount(4);
        friday.Select(s => s.GroupKey).Should().OnlyHaveUniqueItems();
        friday.Select(s => s.Room).Should().OnlyHaveUniqueItems();
        friday.Should().OnlyContain(s => !s.IsMerged);
    }

    [Fact]
    public async Task RealLv1_Import_CreatesTenLanguageGroups_WithTheirExactSessionCounts()
    {
        await using var db = await CreateDbAsync();
        await SyncAsync(db, RealLv1, GroupType.Language);

        (await db.Sessions.CountAsync()).Should().Be(145);
        (await db.Groups.CountAsync()).Should().Be(10);
        (await db.Groups.AllAsync(g => g.Type == GroupType.Language)).Should().BeTrue();

        // Anglais renforcé : 2 cours/semaine, moins les semaines incomplètes de l'export.
        (await SessionCountForGroupAsync(db, "A-I3002TR-AR51")).Should().Be(20);
        (await SessionCountForGroupAsync(db, "A-I3002TR-AR52")).Should().Be(19);
        (await SessionCountForGroupAsync(db, "A-I3002TR-AR53")).Should().Be(20);
        (await SessionCountForGroupAsync(db, "A-I3002TR-AR54")).Should().Be(20);
        (await SessionCountForGroupAsync(db, "A-I3002TR-AR55")).Should().Be(18);

        // Anglais non renforcé : 1 cours/semaine sur 10 semaines.
        foreach (var n in new[] { 1, 2, 3, 4, 5 })
            (await SessionCountForGroupAsync(db, $"A-I3002TR-AN5{n}")).Should().Be(10);
    }

    [Fact]
    public async Task RealLv1_TheSharedToeicTargetsBothGroups_AndOnlyThem()
    {
        await using var db = await CreateDbAsync();

        var ar51 = await DeclareGroupAsync(db, "A-I3002TR-AR51", GroupType.Language);
        var ar52 = await DeclareGroupAsync(db, "A-I3002TR-AR52", GroupType.Language);
        var ar53 = await DeclareGroupAsync(db, "A-I3002TR-AR53", GroupType.Language);

        db.Users.AddRange(
            new User { Id = 1, StudentNumber = "p1", Name = "Dans", Firstname = "AR52", Email = "a@x.fr", Year = "3A", SpecializationId = 1, Lv1GroupId = ar52, Signature = "" },
            new User { Id = 2, StudentNumber = "p2", Name = "Dans", Firstname = "AR53", Email = "b@x.fr", Year = "3A", SpecializationId = 1, Lv1GroupId = ar53, Signature = "" });
        await db.SaveChangesAsync();

        await SyncAsync(db, RealLv1, GroupType.Language);

        // Les groupes déclarés à la main sont réutilisés, pas dupliqués.
        (await db.Groups.CountAsync()).Should().Be(10);

        var toeic = await db.Sessions
            .Include(s => s.SessionGroups)
            .Where(s => s.Name.StartsWith("TOEIC BLANC"))
            .ToListAsync();

        // Trois TOEIC blancs dans l'export : deux communs à AR51 + AR52, un propre à AN51.
        toeic.Should().HaveCount(3);

        var shared = toeic.Where(s => s.SessionGroups.Count == 2).ToList();
        shared.Should().HaveCount(2);
        shared.Should().OnlyContain(s =>
            s.SessionGroups.Select(sg => sg.GroupId).OrderBy(id => id)
                .SequenceEqual(new[] { ar51, ar52 }.OrderBy(id => id)));

        var toeicIds = toeic.Select(s => s.Id).ToList();
        (await db.Attendances.CountAsync(a => a.StudentId == 1 && toeicIds.Contains(a.SessionId))).Should().Be(2);
        (await db.Attendances.CountAsync(a => a.StudentId == 2 && toeicIds.Contains(a.SessionId))).Should().Be(0);
    }

    // ================================================================== LV2 mock

    [Fact]
    public void MockLv2_HasTheSameShapeAsTheRealLv1()
    {
        var mock = Read(MockLv2);
        var events = Parse(MockLv2);

        events.Should().HaveCount(70);
        events.Should().OnlyContain(e => e.Professors.Count == 1);
        events.SelectMany(e => e.GroupLabels).Distinct().Should().HaveCount(7);
        events.Count(e => e.GroupLabels.Count > 1).Should().Be(1);

        // Comme l'export réel : aucune ligne de description ne recopie le SUMMARY.
        events.Should().OnlyContain(e => e.GroupLabels.All(l => l.StartsWith("A-I3004TR-")));

        // Le mock reproduit le changement d'heure dans ses horaires UTC.
        mock.Should().Contain("DTSTART:20260908T074500Z");   // mardi 9h45, heure d'été
        mock.Should().Contain("DTSTART:20261103T084500Z");   // mardi 9h45, heure d'hiver
    }

    [Fact]
    public void MockLv2_RejoinsTheSplitCourse_AndKeepsParallelGroupsApart()
    {
        var sessions = Load(MockLv2);

        sessions.Should().HaveCount(69);

        // CH51 le 24/09 : 14h–15h30 puis 15h45–17h15.
        var ch51 = sessions
            .Where(s => s.Date == new DateTime(2026, 9, 24) && s.GroupLabels.Contains("A-I3004TR-CH51"))
            .ToList();
        ch51.Should().ContainSingle();
        ch51[0].Start.Should().Be(new TimeSpan(14, 0, 0));
        ch51[0].End.Should().Be(new TimeSpan(17, 15, 0));

        // Mardi 08/09 9h45 : espagnol ES51, espagnol ES52, allemand AL51 en parallèle.
        sessions
            .Where(s => s.Date == new DateTime(2026, 9, 8) && s.Start == new TimeSpan(9, 45, 0))
            .Should().HaveCount(3);
    }

    [Fact]
    public async Task MockLv2_Import_GivesEachGroupTenSessions()
    {
        await using var db = await CreateDbAsync();
        await SyncAsync(db, MockLv2, GroupType.Language);

        (await db.Sessions.CountAsync()).Should().Be(69);
        (await db.Groups.AllAsync(g => g.Type == GroupType.Language)).Should().BeTrue();

        // ES51 et ES52 : 9 cours + l'oral blanc commun. CH51 : le cours coupé compte pour un.
        foreach (var code in new[] { "ES51", "ES52", "ES53", "AL51", "AL52", "IT51", "CH51" })
            (await SessionCountForGroupAsync(db, $"A-I3004TR-{code}")).Should().Be(10, code);
    }

    // ============================================================= calendrier unique

    [Fact]
    public void SingleCalendar_CarriesBothFamiliesOfCodes_AndKeepsThemApart()
    {
        // Les deux familles cohabitent dans le même export, sans rien qui les distingue
        // hormis le code lui-même. C'est précisément pourquoi l'import ne classe plus
        // les groupes en LV1 / LV2 : l'information n'est nulle part.
        var sessions = LoadText(SingleLanguageCalendar());

        sessions.Should().HaveCount(145 + 69);

        var labels = sessions.SelectMany(s => s.GroupLabels).Distinct().ToList();
        labels.Should().HaveCount(17);
        labels.Count(l => l.StartsWith("A-I3002TR-")).Should().Be(10);
        labels.Count(l => l.StartsWith("A-I3004TR-")).Should().Be(7);

        // Un cours d'anglais et un cours d'espagnol au même créneau restent deux séances :
        // les recoller parce qu'ils viennent du même fichier serait le piège de la fusion.
        sessions
            .Where(s => s.Date == new DateTime(2026, 9, 8) && s.Start == new TimeSpan(9, 45, 0))
            .Should().HaveCount(3);
    }

    [Fact]
    public async Task SingleCalendar_Import_TypesEveryGroupAsLanguage_WithItsExactSessionCounts()
    {
        await using var db = await CreateDbAsync();
        await SyncAsync(db, LoadText(SingleLanguageCalendar()), GroupType.Language);

        (await db.Sessions.CountAsync()).Should().Be(145 + 69);
        (await db.Groups.CountAsync()).Should().Be(17);

        // Aucun tri LV1 / LV2 : un seul type, et donc aucun geste manuel attendu de l'admin.
        (await db.Groups.AllAsync(g => g.Type == GroupType.Language)).Should().BeTrue();

        // Les comptes par groupe sont ceux des deux exports pris séparément : réunir les
        // calendriers ne doit rien changer à ce que reçoit un groupe donné.
        (await SessionCountForGroupAsync(db, "A-I3002TR-AR51")).Should().Be(20);
        (await SessionCountForGroupAsync(db, "A-I3002TR-AR52")).Should().Be(19);
        (await SessionCountForGroupAsync(db, "A-I3002TR-AR55")).Should().Be(18);
        foreach (var n in new[] { 1, 2, 3, 4, 5 })
            (await SessionCountForGroupAsync(db, $"A-I3002TR-AN5{n}")).Should().Be(10);
        foreach (var code in new[] { "ES51", "ES52", "ES53", "AL51", "AL52", "IT51", "CH51" })
            (await SessionCountForGroupAsync(db, $"A-I3004TR-{code}")).Should().Be(10, code);
    }

    [Fact]
    public async Task SingleCalendar_EachStudentGetsTheSessionsOfTheirGroups_WhicheverSlotHoldsThem()
    {
        // Le cœur de la bascule : ce qui compte est l'appartenance de l'étudiant à ses
        // groupes de langue, pas l'emplacement qui la porte. L'étudiant 2 a volontairement
        // ses deux groupes dans l'ordre inverse de l'étudiant 1.
        await using var db = await CreateDbAsync();

        var ar54 = await DeclareGroupAsync(db, "A-I3002TR-AR54", GroupType.Language);
        var an51 = await DeclareGroupAsync(db, "A-I3002TR-AN51", GroupType.Language);
        var ar52 = await DeclareGroupAsync(db, "A-I3002TR-AR52", GroupType.Language);
        var es53 = await DeclareGroupAsync(db, "A-I3004TR-ES53", GroupType.Language);
        var ch51 = await DeclareGroupAsync(db, "A-I3004TR-CH51", GroupType.Language);

        db.Users.AddRange(
            // Anglais renforcé puis espagnol.
            new User { Id = 1, StudentNumber = "p1", Name = "A", Firstname = "A", Email = "1@x.fr", Year = "3A", SpecializationId = 1, Signature = "",
                       Lv1GroupId = ar54, Lv2GroupId = es53 },
            // Chinois (dont le cours coupé en deux) puis anglais : ordre inverse, même résultat.
            new User { Id = 2, StudentNumber = "p2", Name = "B", Firstname = "B", Email = "2@x.fr", Year = "3A", SpecializationId = 1, Signature = "",
                       Lv1GroupId = ch51, Lv2GroupId = an51 },
            // Un seul groupe, concerné par le TOEIC commun à AR51 et AR52.
            new User { Id = 3, StudentNumber = "p3", Name = "C", Firstname = "C", Email = "3@x.fr", Year = "3A", SpecializationId = 1, Signature = "",
                       Lv1GroupId = ar52 },
            // Aucun groupe de langue : ne doit rien recevoir de ce calendrier.
            new User { Id = 4, StudentNumber = "p4", Name = "D", Firstname = "D", Email = "4@x.fr", Year = "3A", SpecializationId = 1, Signature = "" });
        await db.SaveChangesAsync();

        await SyncAsync(db, LoadText(SingleLanguageCalendar()), GroupType.Language);

        // Les groupes déclarés à la main sont réutilisés, pas dupliqués.
        (await db.Groups.CountAsync()).Should().Be(17);

        (await db.Attendances.CountAsync(a => a.StudentId == 1)).Should().Be(20 + 10);
        (await db.Attendances.CountAsync(a => a.StudentId == 2)).Should().Be(10 + 10);
        (await db.Attendances.CountAsync(a => a.StudentId == 3)).Should().Be(19);
        (await db.Attendances.CountAsync(a => a.StudentId == 4)).Should().Be(0);
    }

    [Fact]
    public async Task SingleCalendar_Reimported_DoesNotDuplicateAnything()
    {
        // L'import automatique de nuit repasse sur le même lien : il doit rapprocher les
        // séances par UID, pas en recréer une deuxième série.
        await using var db = await CreateDbAsync();
        var sessions = LoadText(SingleLanguageCalendar());

        await SyncAsync(db, sessions, GroupType.Language);
        await SyncAsync(db, LoadText(SingleLanguageCalendar()), GroupType.Language);

        (await db.Sessions.CountAsync()).Should().Be(145 + 69);
        (await db.Groups.CountAsync()).Should().Be(17);
    }

    [Fact]
    public async Task LanguageCalendar_AndPromoCalendar_NeverEraseEachOther()
    {
        // Le calendrier de langues et celui d'une promo partagent l'année 3A. Sans le
        // périmètre par calendrier (Session.IcsKind), le second import viderait le premier.
        await using var db = await CreateDbAsync();

        await SyncAsync(db, Load("edt_info3a.ics"), GroupType.Sub);
        var promoSessions = await db.Sessions.CountAsync(s => s.IcsKind == GroupType.Sub);
        promoSessions.Should().BeGreaterThan(0);

        await SyncAsync(db, LoadText(SingleLanguageCalendar()), GroupType.Language);

        // Ré-import des langues : l'emploi du temps de promo reste intact.
        await SyncAsync(db, LoadText(SingleLanguageCalendar()), GroupType.Language);

        (await db.Sessions.CountAsync(s => s.IcsKind == GroupType.Sub)).Should().Be(promoSessions);
        (await db.Sessions.CountAsync(s => s.IcsKind == GroupType.Language)).Should().Be(145 + 69);
    }
}
