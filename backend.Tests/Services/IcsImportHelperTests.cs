using backend.Services;
using FluentAssertions;

namespace backend.Tests.Services;

public class IcsImportHelperTests
{
    // ---------- Normalisation du nom de cours (nom de base) ----------

    [Theory]
    // Préfixes '#' + initiales prof + type → nom de base seul
    [InlineData("####Signal CM/TD (EP)", "Signal")]
    [InlineData("####Signal TP (EP)", "Signal")]
    [InlineData("####Signal CC (EP)", "Signal")]
    [InlineData("##Sys Emb TD", "Sys Emb")]
    [InlineData("##Sys Emb CM", "Sys Emb")]
    [InlineData("####Mecatro Laetitia CM", "Mecatro Laetitia")]
    [InlineData("####Mecatro Laetitia TP", "Mecatro Laetitia")]
    // Type après tiret, tiret interne préservé
    [InlineData("Réseaux et systèmes de communication - TD", "Réseaux et systèmes de communication")]
    [InlineData("Réseaux et systèmes de communication - CM", "Réseaux et systèmes de communication")]
    [InlineData("Algorithmie - programmation objet - TP", "Algorithmie - programmation objet")]
    [InlineData("Système d'exploitation - Partie 1 - CM", "Système d'exploitation - Partie 1")]
    [InlineData("Système d'exploitation - Partie 2 TP", "Système d'exploitation - Partie 2")]
    // Type collé / combos / parenthèses
    [InlineData("Maths pour l'info CM/TD", "Maths pour l'info")]
    [InlineData("####Maths pour l'info - CC", "Maths pour l'info")]
    [InlineData("Web Marketing (CM/TD)", "Web Marketing")]
    [InlineData("Analyse données multidimensionnelles (TP)", "Analyse données multidimensionnelles")]
    [InlineData("Méthodes statistiques pour l'ingénieur TP/TT", "Méthodes statistiques pour l'ingénieur")]
    [InlineData("Optimisation discrète TT", "Optimisation discrète")]
    // Initiales prof seules (pas de type)
    [InlineData("Big Data (KB)", "Big Data")]
    [InlineData("Big Data (HE)", "Big Data")]
    // Groupes accolés au type
    [InlineData("Fondts math. TD Gr1", "Fondts math.")]
    [InlineData("Fondts math. CM", "Fondts math.")]
    [InlineData("Python- TD gr1", "Python")]
    [InlineData("Python - TT 1", "Python")]
    [InlineData("Intro.  prog. objet TP G1", "Intro. prog. objet")]
    [InlineData("Fondts. Algo CM -", "Fondts. Algo")]
    // Sans suffixe → inchangé (hors préfixe #)
    [InlineData("#Projet tutoré dev", "Projet tutoré dev")]
    [InlineData("Application informatique Mobile - projet", "Application informatique Mobile - projet")]
    [InlineData("GIT APP", "GIT APP")]
    public void NormalizeCourseName_ReturnsBaseName(string raw, string expected)
    {
        IcsImportHelper.NormalizeCourseName(raw).Should().Be(expected);
    }

    [Fact]
    public void NormalizeCourseName_EmptyOrOnlyMarkers_FallsBack()
    {
        IcsImportHelper.NormalizeCourseName("").Should().Be("Sans titre");
        IcsImportHelper.NormalizeCourseName("###").Should().Be("Sans titre");
    }

    [Fact]
    public void NormalizeCourseName_MergesCmTdTpToSameBase()
    {
        // Le cœur de la demande : les 3 sous-types donnent le même nom de base.
        var a = IcsImportHelper.NormalizeCourseName("####Signal CM/TD (EP)");
        var b = IcsImportHelper.NormalizeCourseName("####Signal TP (EP)");
        var c = IcsImportHelper.NormalizeCourseName("####Signal CC (EP)");
        a.Should().Be(b).And.Be(c);
    }

    // ---------- Extraction des professeurs ----------

    [Fact]
    public void ExtractProfessors_SingleProfessor()
    {
        var desc = "\n1755942390712\nIngénieur POLYTECH 5A Apprentissage Informatique\nBENABDESLEM KHALID\n\n(Exporté le:02/07/2026 10:25)\n";
        var profs = IcsImportHelper.ExtractProfessors(desc);
        profs.Should().ContainSingle();
        profs[0].Should().Be(("BENABDESLEM", "KHALID"));
    }

    [Fact]
    public void ExtractProfessors_WorksEvenWithMojibakePromoLine()
    {
        // Même si la ligne promo est mal encodée ("IngÃ©nieur"), elle contient "5A" (chiffre)
        // donc reste filtrée : le prof est bien extrait indépendamment de l'encodage.
        var desc = "\nIngÃ©nieur POLYTECH 5A Apprentissage Informatique\nELGHAZEL HAYTHAM\n(ExportÃ© le:02/07/2026 10:25)\n";
        var profs = IcsImportHelper.ExtractProfessors(desc);
        profs.Should().ContainSingle();
        profs[0].Should().Be(("ELGHAZEL", "HAYTHAM"));
    }

    [Fact]
    public void ExtractProfessors_TwoProfessors()
    {
        var desc = "\n\n3A-1 Apprentissage Informatique\n3A-2 Apprentissage Informatique\nPIERRON THEO\nHADDAD MOHAMMED\n\n(Exporté le:02/07/2026 10:24)\n";
        var profs = IcsImportHelper.ExtractProfessors(desc);
        profs.Should().HaveCount(2);
        profs[0].Should().Be(("PIERRON", "THEO"));
        profs[1].Should().Be(("HADDAD", "MOHAMMED"));
    }

    [Fact]
    public void ExtractProfessors_MultiWordSurnameIsDeterministic()
    {
        var desc = "\n\nIngénieur POLYTECH 4A Apprentissage Informatique\nCHASSIN DE KERGOMMEAUX LOIC\n\n(Exporté le)\n";
        var profs = IcsImportHelper.ExtractProfessors(desc);
        profs.Should().ContainSingle();
        profs[0].Should().Be(("CHASSIN", "DE KERGOMMEAUX LOIC"));
    }

    [Fact]
    public void ExtractProfessors_NoProfessor_ReturnsEmpty()
    {
        var desc = "\n\nIngénieur POLYTECH 4A Apprentissage Informatique\n\n(Exporté le:02/07/2026 10:24)\n";
        IcsImportHelper.ExtractProfessors(desc).Should().BeEmpty();
    }

    [Fact]
    public void ExtractProfessors_IgnoresGroupePro_NoBogusProfessor()
    {
        // Événement 5A "Journée d'insertion" : "Groupe Pro" ne doit PAS devenir un professeur.
        var desc = "\n\nIngénieur POLYTECH 5A Apprentissage Informatique\nMECA5 Cpro\nGBM5A groupe 20 [Cpro]\nGroupe Pro\nMAT5 Cpro\n\n(Exporté le:02/07/2026 10:25)\n";
        IcsImportHelper.ExtractProfessors(desc).Should().BeEmpty();
    }

    [Fact]
    public void ExtractProfessors_DeduplicatesRepeatedLines()
    {
        var desc = "\nDE MEYER LUCAS\nDE MEYER LUCAS\n";
        var profs = IcsImportHelper.ExtractProfessors(desc);
        profs.Should().ContainSingle();
        profs[0].Should().Be(("DE", "MEYER LUCAS"));
    }

    // ---------- Groupe cible ----------

    [Fact]
    public void ExtractTargetGroup_SingleGroup()
    {
        var desc = "\nINFO 1-A\nINFO 1-B\n3A-1 Apprentissage Informatique\nBASTIEN JEROME\n";
        IcsImportHelper.ExtractTargetGroup(desc).Should().Be("3A-1");
    }

    [Fact]
    public void ExtractTargetGroup_MultipleGroups_ReturnsEmpty()
    {
        var desc = "\n3A-1 Apprentissage Informatique\n3A-2 Apprentissage Informatique\nMORGE MAXIME\n";
        IcsImportHelper.ExtractTargetGroup(desc).Should().BeEmpty();
    }

    // ---------- Fusion des séances (règles métier) ----------

    private static ImportedSession Slot(string name, string room, string prof, int startH, int startM, int endH, int endM, string group = "")
        => new()
        {
            Date = new DateTime(2025, 9, 24),
            Start = new TimeSpan(startH, startM, 0),
            End = new TimeSpan(endH, endM, 0),
            Name = name,
            Room = room,
            ProfId = prof,
            TargetGroup = group
        };

    [Fact]
    public void ApplyBusinessRules_MergesContiguousCmTdTp_SameRoomSameProf()
    {
        // "Signal" en TP (8h45-10h15) puis CM/TD (10h30-12h00), même salle, même prof.
        var input = new List<ImportedSession>
        {
            Slot("Signal", "ISTIL 240", "7", 8, 45, 10, 15),
            Slot("Signal", "ISTIL 240", "7", 10, 30, 12, 0),
        };

        var result = IcsImportHelper.ApplyBusinessRules(input);

        result.Should().ContainSingle();
        result[0].Start.Should().Be(new TimeSpan(8, 45, 0));
        result[0].End.Should().Be(new TimeSpan(12, 0, 0));
        result[0].IsMerged.Should().BeTrue();
    }

    [Fact]
    public void ApplyBusinessRules_DoesNotMerge_WhenDifferentRoom()
    {
        var input = new List<ImportedSession>
        {
            Slot("Signal", "Amphi", "7", 8, 45, 10, 15),
            Slot("Signal", "ISTIL 16", "7", 10, 30, 12, 0),
        };

        IcsImportHelper.ApplyBusinessRules(input).Should().HaveCount(2);
    }

    [Fact]
    public void ApplyBusinessRules_DoesNotMerge_WhenDifferentGroup()
    {
        var input = new List<ImportedSession>
        {
            Slot("Fondts math.", "ISTIL 17", "3", 7, 45, 9, 15, group: "3A-1"),
            Slot("Fondts math.", "ISTIL 17", "3", 9, 30, 11, 0, group: "3A-2"),
        };

        IcsImportHelper.ApplyBusinessRules(input).Should().HaveCount(2);
    }

    [Fact]
    public void ApplyBusinessRules_DoesNotMerge_WhenGapTooLarge()
    {
        var input = new List<ImportedSession>
        {
            Slot("Signal", "ISTIL 240", "7", 8, 45, 10, 15),
            Slot("Signal", "ISTIL 240", "7", 11, 0, 12, 30), // 45 min d'écart
        };

        IcsImportHelper.ApplyBusinessRules(input).Should().HaveCount(2);
    }

    // ---------- Groupes parallèles (le cœur de la question) ----------

    [Fact]
    public void ApplyBusinessRules_ParallelGroups_DifferentCourses_MergeIntoOneWithBothNamesAndProfs()
    {
        // Même créneau : groupe 1 fait "Fondts math." (salle 17, prof 10),
        // groupe 2 fait "Python" (salle 126, prof 20).
        var input = new List<ImportedSession>
        {
            Slot("Fondts math.", "ISTIL 17", "10", 9, 45, 11, 15, group: "3A-1"),
            Slot("Python", "ISTIL 126", "20", 9, 45, 11, 15, group: "3A-2"),
        };

        var result = IcsImportHelper.ApplyBusinessRules(input);

        result.Should().ContainSingle();
        var s = result[0];
        s.Name.Should().Contain("Fondts math.").And.Contain("Python").And.Contain(" / ");
        new[] { s.ProfId, s.ProfId2 }.Should().BeEquivalentTo(new[] { "10", "20" });
        s.Room.Should().Contain("ISTIL 17").And.Contain("ISTIL 126");
        s.TargetGroup.Should().BeEmpty(); // couvre les 2 groupes → toute la promo
        s.IsMerged.Should().BeTrue();
    }

    [Fact]
    public void ApplyBusinessRules_ParallelGroups_SameCourse_NameNotDuplicated_TwoProfs()
    {
        // Même cours pour les 2 groupes en parallèle, 2 salles, 2 profs.
        var input = new List<ImportedSession>
        {
            Slot("Réseaux et systèmes de communication", "ISTIL 17", "10", 9, 45, 11, 15, group: "3A-1"),
            Slot("Réseaux et systèmes de communication", "ISTIL 126", "20", 9, 45, 11, 15, group: "3A-2"),
        };

        var result = IcsImportHelper.ApplyBusinessRules(input);

        result.Should().ContainSingle();
        result[0].Name.Should().Be("Réseaux et systèmes de communication"); // pas "X / X"
        new[] { result[0].ProfId, result[0].ProfId2 }.Should().BeEquivalentTo(new[] { "10", "20" });
    }

    [Fact]
    public void ApplyBusinessRules_ThreeParallelGroups_CombinesNamesAndRooms_KeepsTwoProfs()
    {
        var input = new List<ImportedSession>
        {
            Slot("A", "R1", "1", 9, 0, 11, 0, group: "3A-1"),
            Slot("B", "R2", "2", 9, 0, 11, 0, group: "3A-2"),
            Slot("C", "R3", "3", 9, 0, 11, 0, group: "3A-3"),
        };

        var result = IcsImportHelper.ApplyBusinessRules(input);

        result.Should().ContainSingle();
        result[0].Name.Split(" / ").Should().BeEquivalentTo(new[] { "A", "B", "C" });
        result[0].Room.Split(" / ").Should().HaveCount(3);
        new[] { result[0].ProfId, result[0].ProfId2 }.Where(p => p != "").Should().HaveCount(2); // modèle = 2 profs max
        result[0].TargetGroup.Should().BeEmpty();
    }

    [Fact]
    public void ApplyBusinessRules_SequentialDifferentGroups_SameRoom_AreNotMerged()
    {
        // Deux sous-groupes qui s'enchaînent dans la même salle ne doivent PAS fusionner
        // (émargements distincts par groupe).
        var input = new List<ImportedSession>
        {
            Slot("Init. BD", "ISTIL 128", "5", 8, 0, 10, 0, group: "3A-1"),
            Slot("Init. BD", "ISTIL 128", "5", 10, 0, 12, 0, group: "3A-2"),
        };

        IcsImportHelper.ApplyBusinessRules(input).Should().HaveCount(2);
    }

    [Fact]
    public void ApplyBusinessRules_25Sep_LongCourseSplitOnOtherGroupPause_TwoSessionsTwoProfs()
    {
        // Cas réel 25/09 après-midi :
        //   Groupe 2 : Intro. prog. objet, un seul bloc de 3h15 (14:00-17:15), prof 9.
        //   Groupe 1 : Init. BD 14:00-15:30 (prof 13) PUIS Python 15:45-17:15 (prof 8), pause 15 min.
        // Attendu : 2 sessions, chacune à 2 profs, le cours long du G2 étant découpé sur la pause du G1.
        var input = new List<ImportedSession>
        {
            Slot("Intro. prog. objet", "ISTIL 240", "9", 14, 0, 17, 15, group: "3A-2"),
            Slot("Init. BD", "ISTIL 128", "13", 14, 0, 15, 30, group: "3A-1"),
            Slot("Python", "ISTIL 128", "8", 15, 45, 17, 15, group: "3A-1"),
        };

        var r = IcsImportHelper.ApplyBusinessRules(input).OrderBy(s => s.Start).ToList();

        r.Should().HaveCount(2);

        r[0].Start.Should().Be(new TimeSpan(14, 0, 0));
        r[0].End.Should().Be(new TimeSpan(15, 30, 0));
        r[0].Name.Split(" / ").Should().BeEquivalentTo(new[] { "Init. BD", "Intro. prog. objet" });
        new[] { r[0].ProfId, r[0].ProfId2 }.Should().BeEquivalentTo(new[] { "13", "9" });
        r[0].TargetGroup.Should().BeEmpty();

        r[1].Start.Should().Be(new TimeSpan(15, 45, 0));
        r[1].End.Should().Be(new TimeSpan(17, 15, 0));
        r[1].Name.Split(" / ").Should().BeEquivalentTo(new[] { "Python", "Intro. prog. objet" });
        new[] { r[1].ProfId, r[1].ProfId2 }.Should().BeEquivalentTo(new[] { "8", "9" });
        r[1].TargetGroup.Should().BeEmpty();
    }

    [Fact]
    public void ApplyBusinessRules_UnequalDurations_OneGroupOnly_NotSplit_NotMerged()
    {
        // G1 : X de 1h30 ; G2 : Y de 3h (un bloc). Y n'est PAS recouvert par 2 séances du G1
        // → pas de découpage, et créneaux différents → pas de fusion. Chaque groupe garde sa séance.
        var input = new List<ImportedSession>
        {
            Slot("X", "R1", "A", 8, 0, 9, 30, group: "3A-1"),
            Slot("Y", "R2", "B", 8, 0, 11, 0, group: "3A-2"),
        };

        var r = IcsImportHelper.ApplyBusinessRules(input).OrderBy(s => s.End).ToList();

        r.Should().HaveCount(2);
        r[0].Name.Should().Be("X");
        r[0].End.Should().Be(new TimeSpan(9, 30, 0));
        r[1].Name.Should().Be("Y");
        r[1].End.Should().Be(new TimeSpan(11, 0, 0));
        r.Should().OnlyContain(s => !s.Name.Contains(" / ")); // aucune fusion
    }

    // ---------- Parsing complet d'un ICS (bout en bout, sans réseau) ----------

    [Fact]
    public void ParseCalendar_RealEvents_NormalizesNamesAndExtractsProfessors()
    {
        // 3 VEVENT représentatifs des ICS fournis (accents corrects, comme après décodage UTF-8).
        const string ics =
            "BEGIN:VCALENDAR\r\nVERSION:2.0\r\nPRODID:-//test//\r\n" +
            "BEGIN:VEVENT\r\nUID:1\r\nDTSTART:20250930T060000Z\r\nDTEND:20250930T073000Z\r\n" +
            "SUMMARY:Big Data (KB)\r\nLOCATION:Salle ISTIL 21\r\n" +
            "DESCRIPTION:\\n1755942390712\\nIngénieur POLYTECH 5A Apprentissage Informatique\\nBENABDESLEM KHALID\\n\\n(Exporté le:02/07/2026 10:25)\\n\r\n" +
            "END:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nUID:2\r\nDTSTART:20260306T103000Z\r\nDTEND:20260306T120000Z\r\n" +
            "SUMMARY:####Signal TP (EP)\r\nLOCATION:Salle ISTIL 240 Learning Lab\r\n" +
            "DESCRIPTION:\\n\\nIngénieur POLYTECH 5A Apprentissage Informatique\\nPERRIN EMMANUEL\\n\\n(Exporté le)\\n\r\n" +
            "END:VEVENT\r\n" +
            "BEGIN:VEVENT\r\nUID:3\r\nDTSTART:20260519T060000Z\r\nDTEND:20260519T160000Z\r\n" +
            "SUMMARY:Journée dinsertion pro - 5A CPRO et FISA\r\nLOCATION:Salle ISTIL 22\r\n" +
            "DESCRIPTION:\\n\\nIngénieur POLYTECH 5A Apprentissage Informatique\\nMECA5 Cpro\\nGBM5A groupe 20 [Cpro]\\nGroupe Pro\\nMAT5 Cpro\\n\\n(Exporté le)\\n\r\n" +
            "END:VEVENT\r\n" +
            "END:VCALENDAR\r\n";

        var events = IcsImportHelper.ParseCalendar(ics);

        events.Should().HaveCount(3);

        var bigData = events.Single(e => e.Name == "Big Data");
        bigData.Professors.Should().ContainSingle().Which.Should().Be(("BENABDESLEM", "KHALID"));

        var signal = events.Single(e => e.Name == "Signal"); // "####Signal TP (EP)" → "Signal"
        signal.Professors.Should().ContainSingle().Which.Should().Be(("PERRIN", "EMMANUEL"));

        // L'événement "Journée d'insertion" ne doit créer AUCUN professeur ("Groupe Pro" ignoré).
        var insertion = events.Single(e => e.Name.StartsWith("Journée"));
        insertion.Professors.Should().BeEmpty();
    }
}
