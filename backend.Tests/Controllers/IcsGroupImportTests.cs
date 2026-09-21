using System.Reflection;
using System.Text;
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
/// Import bout en bout des trois calendriers d'une promotion : EDT, LV1 et LV2.
///
/// Les fixtures reproduisent les formes réellement observées dans les exports ADE de
/// Polytech Lyon : ciblage par libellé promo, par série de 8 sous-groupes, par sous-ensemble
/// arbitraire (les TP à 5/6/5 à cheval sur les deux séries), cours mutualisé entre filières,
/// cours coupé en 2×1h30, et code de groupe opaque pour les langues.
///
/// Ce qui est vérifié ici, c'est la chaîne complète : libellés lus verbatim → groupes créés →
/// séances rattachées → QUI doit émarger sur quoi.
/// </summary>
public class IcsGroupImportTests
{
    private const string InfoPromoLabel = "Diplôme d'Ingénieur POLYTECH 3A (Informatique)";

    private const int InfoSpecId = 1;
    private const int LanguesSpecId = 2;
    private const int MecaSpecId = 3;

    // Étudiants du scénario.
    private const int Alice = 1;   // INFO 1-A, LV1 groupe A
    private const int Bob = 2;     // INFO 2-D, LV2 groupe A
    private const int Chloe = 3;   // INFO 3A sans sous-groupe (cas historique)
    private const int David = 4;   // INFO 1-F, aucune langue
    private const int Eve = 5;     // autre filière

    private const string Lv1GroupA = "A-PL9003TR-BE91";
    private const string Lv2GroupA = "A-PL9004TR-BE91";

    // ------------------------------------------------------------------ outillage

    private static string FixturePath(string name)
        => Path.Combine(AppContext.BaseDirectory, "Fixtures", name);

    private static ImportController BuildController(backend.Data.ApplicationDbContext db)
    {
        var services = new ServiceCollection();
        services.AddHttpClient();
        var provider = services.BuildServiceProvider();
        return new ImportController(
            db,
            NullLogger<ImportController>.Instance,
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<IHttpClientFactory>());
    }

    /// <summary>
    /// Décale toutes les séances d'un import pour qu'elles soient à venir.
    ///
    /// Les fixtures portent des dates fixes (septembre 2026). Les scénarios qui
    /// dépendent du caractère FUTUR d'une séance — le retrait d'un émargement n'est
    /// permis que sur une séance à venir et encore vierge — doivent donc les décaler,
    /// sinon ils cessent de tester ce qu'ils prétendent dès que la date est dépassée.
    /// </summary>
    private static List<ImportedSession> ShiftToFuture(List<ImportedSession> sessions)
    {
        if (sessions.Count == 0) return sessions;

        var earliest = sessions.Min(s => s.Date);
        var offset = DateTime.Today.AddDays(7) - earliest;

        foreach (var session in sessions)
            session.Date = session.Date.Add(offset);

        return sessions;
    }

    /// <summary>
    /// Reproduit ce que fait <c>FetchAndParseIcs</c> après le téléchargement, sans réseau
    /// ni résolution des professeurs (testée ailleurs) : parse le fichier puis applique
    /// les règles métier de fusion.
    /// </summary>
    private static List<ImportedSession> LoadFixture(string name, string year)
    {
        var content = File.ReadAllText(FixturePath(name), Encoding.UTF8).TrimStart('﻿');

        var sessions = IcsImportHelper.ParseCalendar(content)
            .Select(ev => new ImportedSession
            {
                Date = ev.Date,
                Start = ev.Start,
                End = ev.End,
                Name = ev.Name,
                Room = ev.Room,
                ProfId = "",
                ProfId2 = "",
                Year = year,
                Uid = ev.Uid,
                GroupLabels = ev.GroupLabels
            })
            .ToList();

        return IcsImportHelper.ApplyBusinessRules(sessions);
    }

    private static async Task SyncAsync(
        ImportController controller, List<ImportedSession> imported,
        string year, int specializationId, GroupType kind, string? promoLabel)
    {
        var sync = typeof(ImportController).GetMethod(
            "SyncWithDatabase", BindingFlags.NonPublic | BindingFlags.Instance);
        sync.Should().NotBeNull();
        await (Task)sync!.Invoke(controller, new object?[] { imported, year, specializationId, kind, promoLabel })!;
    }

    /// <summary>
    /// Base de départ : les filières, les groupes déjà connus (situation normale une fois
    /// le premier import passé et l'Excel importé) et les étudiants affectés.
    /// Le libellé promo, lui, n'est PAS pré-créé : c'est l'import qui doit le découvrir.
    /// </summary>
    private static async Task<backend.Data.ApplicationDbContext> SeedAsync()
    {
        var db = DbContextHelper.CreateInMemoryDbContext();

        db.Specializations.AddRange(
            new Specialization { Id = InfoSpecId, Name = "Informatique", Code = "INFO" },
            new Specialization { Id = LanguesSpecId, Name = "Langues", Code = "LANGUES" },
            new Specialization { Id = MecaSpecId, Name = "Mécanique", Code = "MECA" });

        var id = 100;
        foreach (var serie in new[] { 1, 2 })
        {
            foreach (var letter in "ABCDEFGH")
            {
                db.Groups.Add(new Group
                {
                    Id = id++,
                    Label = $"INFO {serie}-{letter}",
                    DisplayName = $"INFO {serie}-{letter}",
                    Type = GroupType.Sub,
                    SpecializationId = InfoSpecId,
                    Year = "3A"
                });
            }
        }

        db.Groups.AddRange(
            new Group { Id = 200, Label = Lv1GroupA, DisplayName = "Anglais A", Type = GroupType.Lv1, SpecializationId = LanguesSpecId, Year = "3A" },
            new Group { Id = 201, Label = "B-PL9003TR-BE92", DisplayName = "Anglais B", Type = GroupType.Lv1, SpecializationId = LanguesSpecId, Year = "3A" },
            new Group { Id = 202, Label = Lv2GroupA, DisplayName = "Espagnol A", Type = GroupType.Lv2, SpecializationId = LanguesSpecId, Year = "3A" },
            new Group { Id = 203, Label = "B-PL9004TR-BE92", DisplayName = "Allemand B", Type = GroupType.Lv2, SpecializationId = LanguesSpecId, Year = "3A" });

        await db.SaveChangesAsync();

        var infoA = await db.Groups.SingleAsync(g => g.Label == "INFO 1-A");
        var info2D = await db.Groups.SingleAsync(g => g.Label == "INFO 2-D");
        var info1F = await db.Groups.SingleAsync(g => g.Label == "INFO 1-F");

        db.Users.AddRange(
            new User { Id = Alice, StudentNumber = "p1", Name = "Alice", Firstname = "A", Email = "alice@x.fr", Year = "3A", SpecializationId = InfoSpecId, SubGroupId = infoA.Id, Lv1GroupId = 200 },
            new User { Id = Bob, StudentNumber = "p2", Name = "Bob", Firstname = "B", Email = "bob@x.fr", Year = "3A", SpecializationId = InfoSpecId, SubGroupId = info2D.Id, Lv2GroupId = 202 },
            new User { Id = Chloe, StudentNumber = "p3", Name = "Chloe", Firstname = "C", Email = "chloe@x.fr", Year = "3A", SpecializationId = InfoSpecId },
            new User { Id = David, StudentNumber = "p4", Name = "David", Firstname = "D", Email = "david@x.fr", Year = "3A", SpecializationId = InfoSpecId, SubGroupId = info1F.Id },
            new User { Id = Eve, StudentNumber = "p5", Name = "Eve", Firstname = "E", Email = "eve@x.fr", Year = "3A", SpecializationId = MecaSpecId });

        await db.SaveChangesAsync();
        return db;
    }

    private static async Task ImportEdtAsync(backend.Data.ApplicationDbContext db)
    {
        await SyncAsync(BuildController(db), LoadFixture("edt_info3a.ics", "3A"),
            "3A", InfoSpecId, GroupType.Sub, InfoPromoLabel);
    }

    private static async Task<List<string>> SessionNamesForAsync(
        backend.Data.ApplicationDbContext db, int studentId)
        => await db.Attendances
            .Where(a => a.StudentId == studentId)
            .Join(db.Sessions, a => a.SessionId, s => s.Id, (a, s) => s.Name)
            .OrderBy(n => n)
            .ToListAsync();

    // ------------------------------------------------------------------ EDT de promo

    [Fact]
    public async Task Edt_ParallelSubGroupsOnSameSlot_StayDistinctSessions()
    {
        await using var db = await SeedAsync();
        await ImportEdtAsync(db);

        // "Fondts math. TD Gr1" et "TD Gr2" tombent au même créneau et se normalisent au
        // même nom : seules les salles et les groupes les distinguent. L'ancien code les
        // fusionnait en une séance "A / B" à deux profs.
        var fondts = await db.Sessions.Where(s => s.Name == "Fondts math.").ToListAsync();
        fondts.Should().HaveCount(2);
        fondts.Select(s => s.Room).Should().OnlyHaveUniqueItems();

        // Les 3 TP parallèles (5/6/5 sous-groupes) restent eux aussi 3 séances.
        var tp = await db.Sessions.Where(s => s.Name == "Informatique 1").ToListAsync();
        tp.Should().HaveCount(3);
        tp.Select(s => s.Room).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task Edt_ContiguousSlotsOfSameCourseAndSameAudience_AreMerged()
    {
        await using var db = await SeedAsync();
        await ImportEdtAsync(db);

        var initBd = await db.Sessions.SingleAsync(s => s.Name == "Init. BD");
        initBd.IsMerged.Should().BeTrue();
        initBd.StartTime.TimeOfDay.Should().Be(new TimeSpan(8, 0, 0));
        initBd.EndTime.TimeOfDay.Should().Be(new TimeSpan(11, 15, 0));
    }

    [Fact]
    public async Task Edt_DiscoversPromoLabel_AndForeignLabelsWithoutSideEffect()
    {
        await using var db = await SeedAsync();
        await ImportEdtAsync(db);

        var promo = await db.Groups.SingleAsync(g => g.Label == InfoPromoLabel);
        promo.Type.Should().Be(GroupType.Promo);
        promo.SpecializationId.Should().Be(InfoSpecId);
        promo.Year.Should().Be("3A");

        // Le cours mutualisé fait apparaître les libellés des 4 autres filières. Ils sont
        // créés — jamais rejetés, sinon un renommage ADE ferait disparaître des séances —
        // mais restent sans effet puisque aucun étudiant ne leur est rattaché.
        var gbm = await db.Groups.SingleAsync(g => g.Label.Contains("Génie Biomédical"));
        gbm.Type.Should().Be(GroupType.Sub);
        (await db.Users.CountAsync(u => u.SubGroupId == gbm.Id)).Should().Be(0);

        // Les marqueurs d'enseignant non affecté ne deviennent jamais des groupes.
        (await db.Groups.AnyAsync(g => g.Label == "." || g.Label == "???")).Should().BeFalse();
    }

    [Fact]
    public async Task Edt_EachSessionCarriesItsExactTargetGroups()
    {
        await using var db = await SeedAsync();
        await ImportEdtAsync(db);

        var tpG2 = await db.Sessions
            .Include(s => s.SessionGroups).ThenInclude(sg => sg.Group)
            .SingleAsync(s => s.Name == "Informatique 1" && s.Room.Contains("240"));

        tpG2.SessionGroups.Select(sg => sg.Group.Label)
            .Should().BeEquivalentTo("INFO 1-F", "INFO 1-G", "INFO 1-H",
                                     "INFO 2-A", "INFO 2-B", "INFO 2-C");
    }

    [Fact]
    public async Task Edt_AttendanceFollowsTheStudentSubGroup()
    {
        await using var db = await SeedAsync();
        await ImportEdtAsync(db);

        (await db.Sessions.CountAsync()).Should().Be(8);

        // Alice (INFO 1-A) : le CM de promo, le TD de sa série, le cours mutualisé,
        // le TP recollé de sa série et le TP du groupe 1-A..1-E.
        (await SessionNamesForAsync(db, Alice)).Should().BeEquivalentTo(
            "Algo. prog. Objet MM", "Fondts math.", "Jeu d'entreprise", "Init. BD", "Informatique 1");

        // Bob (INFO 2-D) : l'autre TD, l'autre TP, et pas de "Init. BD" (série 1 seulement).
        (await SessionNamesForAsync(db, Bob)).Should().BeEquivalentTo(
            "Algo. prog. Objet MM", "Fondts math.", "Jeu d'entreprise", "Informatique 1");

        // David (INFO 1-F) : même série qu'Alice pour le TD, mais l'autre groupe de TP.
        var davidTp = await db.Attendances
            .Where(a => a.StudentId == David)
            .Join(db.Sessions, a => a.SessionId, s => s.Id, (a, s) => s)
            .SingleOrDefaultAsync(s => s.Name == "Informatique 1");
        davidTp!.Room.Should().Contain("240");
    }

    [Fact]
    public async Task Edt_StudentWithoutSubGroup_StillGetsEverything()
    {
        await using var db = await SeedAsync();
        await ImportEdtAsync(db);

        // Règle de compatibilité : tant qu'un étudiant n'a pas de sous-groupe, il reçoit
        // toutes les séances de son année et de sa filière, comme avant les groupes.
        // C'est ce qui rend la bascule transparente pour une promo déjà en production.
        (await db.Attendances.CountAsync(a => a.StudentId == Chloe)).Should().Be(8);

        // Une étudiante d'une autre filière n'est jamais concernée.
        (await db.Attendances.CountAsync(a => a.StudentId == Eve)).Should().Be(0);
    }

    // ------------------------------------------------------------------ langues

    [Fact]
    public async Task Lv1AndLv2_AreImportedUnderTheirOwnSpecialization_AndOnlyReachTheirGroup()
    {
        await using var db = await SeedAsync();
        await ImportEdtAsync(db);

        var controller = BuildController(db);
        await SyncAsync(controller, LoadFixture("lv1.ics", "3A"), "3A", LanguesSpecId, GroupType.Lv1, null);
        await SyncAsync(controller, LoadFixture("lv2.ics", "3A"), "3A", LanguesSpecId, GroupType.Lv2, null);

        (await db.Sessions.CountAsync(s => s.SpecializationId == LanguesSpecId)).Should().Be(4);

        // Alice suit l'anglais du groupe A : elle gagne exactement une séance de langue.
        var aliceLanguage = await db.Attendances
            .Where(a => a.StudentId == Alice)
            .Join(db.Sessions, a => a.SessionId, s => s.Id, (a, s) => s)
            .Where(s => s.SpecializationId == LanguesSpecId)
            .ToListAsync();
        aliceLanguage.Should().ContainSingle().Which.Name.Should().Contain("Anglais Gr A");

        // Bob suit l'espagnol du groupe A.
        var bobLanguage = await db.Attendances
            .Where(a => a.StudentId == Bob)
            .Join(db.Sessions, a => a.SessionId, s => s.Id, (a, s) => s)
            .Where(s => s.SpecializationId == LanguesSpecId)
            .ToListAsync();
        bobLanguage.Should().ContainSingle().Which.Name.Should().Contain("Espagnol Gr A");

        // David n'a aucune langue : il ne reçoit rien. Chloe non plus — le repli
        // "étudiant non affecté" est borné à SA filière, il ne l'aspire pas dans les langues.
        (await db.Attendances.CountAsync(a =>
            a.StudentId == David &&
            db.Sessions.Any(s => s.Id == a.SessionId && s.SpecializationId == LanguesSpecId)))
            .Should().Be(0);
        (await db.Attendances.CountAsync(a =>
            a.StudentId == Chloe &&
            db.Sessions.Any(s => s.Id == a.SessionId && s.SpecializationId == LanguesSpecId)))
            .Should().Be(0);
    }

    [Fact]
    public async Task Lv_OverlappingTheSubGroupCourse_CoexistsAsTwoEnrolments()
    {
        await using var db = await SeedAsync();
        await ImportEdtAsync(db);
        await SyncAsync(BuildController(db), LoadFixture("lv1.ics", "3A"), "3A", LanguesSpecId, GroupType.Lv1, null);

        // Le créneau d'anglais d'Alice recouvre exactement le TD de sa série : elle est
        // bien inscrite aux deux, et c'est le "cours en cours" qui doit trancher.
        var overlapping = await db.Attendances
            .Where(a => a.StudentId == Alice)
            .Join(db.Sessions, a => a.SessionId, s => s.Id, (a, s) => s)
            .Where(s => s.Date == new DateTime(2026, 9, 22))
            .ToListAsync();

        overlapping.Should().HaveCount(2);
        overlapping.Select(s => s.StartTime.TimeOfDay).Should().AllBeEquivalentTo(new TimeSpan(7, 45, 0));
    }

    // ------------------------------------------------------------------ ré-imports

    [Fact]
    public async Task Reimport_IsIdempotent()
    {
        await using var db = await SeedAsync();
        await ImportEdtAsync(db);

        var sessions = await db.Sessions.CountAsync();
        var groups = await db.Groups.CountAsync();
        var attendances = await db.Attendances.CountAsync();

        await ImportEdtAsync(db);

        (await db.Sessions.CountAsync()).Should().Be(sessions);
        (await db.Groups.CountAsync()).Should().Be(groups);
        (await db.Attendances.CountAsync()).Should().Be(attendances);
    }

    [Fact]
    public async Task Reimport_WhenSlotMoves_KeepsTheSessionAndItsSavedAttendance()
    {
        await using var db = await SeedAsync();
        await ImportEdtAsync(db);

        var before = await db.Sessions.SingleAsync(s => s.Name == "Algo. prog. Objet MM");
        // Valeurs figées : `before` est suivi par EF et sera muté en place par le ré-import.
        var originalId = before.Id;
        var originalStart = before.StartTime.TimeOfDay;

        var attendance = await db.Attendances.SingleAsync(a => a.SessionId == before.Id && a.StudentId == Alice);
        attendance.Status = AttendanceStatus.Present;
        await db.SaveChangesAsync();

        // ADE décale le cours d'une heure. Le rapprochement historique se faisait sur
        // (date, début, fin) : la séance était supprimée puis recréée, et l'émargement
        // déjà saisi disparaissait. Le rapprochement par UID le préserve.
        var moved = LoadFixture("edt_info3a.ics", "3A");
        var target = moved.Single(s => s.Name == "Algo. prog. Objet MM");
        target.Start = target.Start.Add(TimeSpan.FromHours(1));
        target.End = target.End.Add(TimeSpan.FromHours(1));

        await SyncAsync(BuildController(db), moved, "3A", InfoSpecId, GroupType.Sub, InfoPromoLabel);

        var after = await db.Sessions.SingleAsync(s => s.Name == "Algo. prog. Objet MM");
        after.Id.Should().Be(originalId);
        after.StartTime.TimeOfDay.Should().Be(originalStart.Add(TimeSpan.FromHours(1)));

        (await db.Attendances.SingleAsync(a => a.SessionId == after.Id && a.StudentId == Alice))
            .Status.Should().Be(AttendanceStatus.Present);
    }

    [Fact]
    public async Task Reimport_WhenTargetGroupsChange_AttendanceIsRecomputed()
    {
        await using var db = await SeedAsync();

        // Séances décalées dans le futur : le retrait d'un émargement devenu hors
        // périmètre ne s'applique qu'aux séances à venir, jamais à l'historique.
        await SyncAsync(BuildController(db), ShiftToFuture(LoadFixture("edt_info3a.ics", "3A")),
            "3A", InfoSpecId, GroupType.Sub, InfoPromoLabel);

        var tpG1 = await db.Sessions.SingleAsync(s => s.Name == "Informatique 1" && s.Room.Contains("123"));
        (await db.Attendances.AnyAsync(a => a.SessionId == tpG1.Id && a.StudentId == Alice)).Should().BeTrue();
        (await db.Attendances.AnyAsync(a => a.SessionId == tpG1.Id && a.StudentId == Bob)).Should().BeFalse();

        // Le TP bascule du groupe 1-A..1-E vers 2-A..2-D : Bob entre, Alice sort.
        var changed = ShiftToFuture(LoadFixture("edt_info3a.ics", "3A"));
        var target = changed.Single(s => s.Name == "Informatique 1" && s.Room.Contains("123"));
        target.GroupLabels = new List<string> { "INFO 2-A", "INFO 2-B", "INFO 2-C", "INFO 2-D" };

        await SyncAsync(BuildController(db), changed, "3A", InfoSpecId, GroupType.Sub, InfoPromoLabel);

        (await db.Attendances.AnyAsync(a => a.SessionId == tpG1.Id && a.StudentId == Bob)).Should().BeTrue();
        (await db.Attendances.AnyAsync(a => a.SessionId == tpG1.Id && a.StudentId == Alice)).Should().BeFalse();
    }
}
