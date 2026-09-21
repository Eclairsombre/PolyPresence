using backend.Services;
using FluentAssertions;

namespace backend.Tests.Services;

/// <summary>
/// Lecture des libellés de groupe dans la description ADE.
///
/// La description n'étiquette rien : sa structure est purement positionnelle (identifiant
/// numérique, groupes, éventuellement le nom du cours, puis les enseignants en suffixe).
/// Ces cas reproduisent les formes réellement observées dans les exports INFO 3A, MECA 3A
/// et LV2 — notamment le fait que les conventions de nommage ne sont PAS uniformes.
/// </summary>
public class IcsGroupLabelTests
{
    [Fact]
    public void ExtractGroupLabels_PromoWideEvent_ReturnsThePromoLabel()
    {
        // Forme la plus fréquente : 185 des 298 séances du vrai calendrier INFO 3A.
        var desc = "\n1787834880149\nDiplôme d'Ingénieur POLYTECH 3A (Informatique)\nCHASSIN DE KERGOMMEAUX LOIC\n\n(Exporté le:04/09/2026 14:19)\n";

        IcsImportHelper.ExtractGroupLabels(desc, "Algo  & complexité TD LC")
            .Should().Equal("Diplôme d'Ingénieur POLYTECH 3A (Informatique)");
    }

    [Fact]
    public void ExtractGroupLabels_SubGroupEvent_ReturnsEveryTargetedSubGroup()
    {
        var desc = "\n1787834837741\nINFO 1-A\nINFO 1-B\nINFO 1-C\nINFO 1-D\nINFO 1-E\nINFO 1-F\nINFO 1-G\nINFO 1-H\nPIERRON THEO\n\n(Exporté le)\n";

        IcsImportHelper.ExtractGroupLabels(desc, "Fondts math. TD Gr1")
            .Should().Equal("INFO 1-A", "INFO 1-B", "INFO 1-C", "INFO 1-D",
                            "INFO 1-E", "INFO 1-F", "INFO 1-G", "INFO 1-H");
    }

    [Fact]
    public void ExtractGroupLabels_ArbitrarySubset_IsPreservedExactly()
    {
        // Cas MECA 3A : les TP découpent la promo en 5/6/5, à cheval sur les deux séries.
        // C'est ce qui interdit tout raccourci "série 1 / série 2".
        var desc = "\n1787834841843\nINFO 1-F\nINFO 1-G\nINFO 1-H\nINFO 2-A\nINFO 2-B\nINFO 2-C\nREYMERMIER THEO\n";

        IcsImportHelper.ExtractGroupLabels(desc, "Informatique 1 (TP - G2)")
            .Should().Equal("INFO 1-F", "INFO 1-G", "INFO 1-H", "INFO 2-A", "INFO 2-B", "INFO 2-C");
    }

    [Fact]
    public void ExtractGroupLabels_LanguageEvent_DropsTheCourseNameLine()
    {
        // Les ICS de langues répètent le nom du cours entre le code de groupe et le prof.
        var desc = "\n1787834841843\nA-PL9004TR-BE91\nS9 - Espagnol Gr A (Mar 9h45-13h) E. PRESSIAT\nPRESSIAT EMILIA\n\n(Exporté le)\n";

        IcsImportHelper.ExtractGroupLabels(desc, "S9 - Espagnol Gr A (Mar 9h45-13h) E. PRESSIAT")
            .Should().Equal("A-PL9004TR-BE91");
    }

    [Fact]
    public void ExtractGroupLabels_WithoutSummary_KeepsTheCourseNameLine()
    {
        // Sans SUMMARY on ne peut pas distinguer le nom du cours d'un groupe : on préfère
        // un libellé en trop (inoffensif, aucun étudiant n'y est rattaché) à une perte.
        var desc = "\n123\nA-PL9004TR-BE91\nS9 - Espagnol Gr A\nPRESSIAT EMILIA\n";

        IcsImportHelper.ExtractGroupLabels(desc)
            .Should().Equal("A-PL9004TR-BE91", "S9 - Espagnol Gr A");
    }

    [Fact]
    public void ExtractGroupLabels_UnassignedTeacherMarkers_AreNotGroups()
    {
        // "." , "???" et "??? - 50%" sont des enseignants non affectés : 45 occurrences sur
        // les calendriers réels. Sans filtre, ils deviendraient des groupes.
        var desc = "\n1787834880149\nDiplôme d'Ingénieur POLYTECH 3A (Informatique)\nARCY HAMID\n.\n???\n??? - 50%\nHOFMANN BERND\n";

        IcsImportHelper.ExtractGroupLabels(desc, "Jeu d'entreprise")
            .Should().Equal("Diplôme d'Ingénieur POLYTECH 3A (Informatique)");
    }

    [Theory]
    // Aucune convention commune : quatre nommages coexistent la même année.
    [InlineData("INFO 1-A")]
    [InlineData("INFO5 A")]
    [InlineData("MAT5 A")]
    [InlineData("MECA5 1-A")]
    [InlineData("GBM5A groupe 07 [Ing]")]
    [InlineData("3A-1 Apprentissage Informatique")]
    [InlineData("A-PL9004TR-BE91")]
    [InlineData("Groupe Principal")]
    public void ExtractGroupLabels_KeepsAnyNamingConventionVerbatim(string label)
    {
        var desc = $"\n1787834880149\n{label}\nMORGE MAXIME\n\n(Exporté le)\n";

        IcsImportHelper.ExtractGroupLabels(desc, "Peu importe").Should().Equal(label);
    }

    [Fact]
    public void ExtractGroupLabels_MutualisedEvent_ReturnsEveryPromoIncludingOtherSpecializations()
    {
        // Un cours mutualisé liste les 5 filières. Les libellés étrangers sont conservés :
        // ils créeront des groupes sans étudiant, donc sans effet, et se rattacheront
        // d'eux-mêmes quand la filière concernée sera importée à son tour.
        var desc = "\n1787834880149\nDiplôme d'Ingénieur POLYTECH 3A (Informatique)\n"
                   + "Diplôme d'Ingénieur POLYTECH 3A (Maths appli et mod)\n"
                   + "Diplôme d'Ingénieur POLYTECH 3A (Matériaux)\nGHRENASSIA EDMOND\n.\n???\n";

        IcsImportHelper.ExtractGroupLabels(desc, "Jeu d'entreprise").Should().HaveCount(3);
    }

    [Fact]
    public void ExtractGroupLabels_DeduplicatesRepeatedLabels()
    {
        var desc = "\n123\nINFO 1-A\nINFO 1-A\nINFO 1-B\n";

        IcsImportHelper.ExtractGroupLabels(desc).Should().Equal("INFO 1-A", "INFO 1-B");
    }

    [Fact]
    public void ExtractGroupLabels_EmptyDescription_ReturnsEmpty()
    {
        IcsImportHelper.ExtractGroupLabels(null).Should().BeEmpty();
        IcsImportHelper.ExtractGroupLabels("").Should().BeEmpty();
        IcsImportHelper.ExtractGroupLabels("\n\n(Exporté le:04/09/2026 14:19)\n").Should().BeEmpty();
    }

    [Theory]
    [InlineData("1787834880149", true)]   // identifiant ADE
    [InlineData("???", true)]
    [InlineData(".", true)]
    [InlineData("??? - 50%", true)]
    [InlineData("(Exporté le:04/09/2026 14:19)", true)]
    [InlineData("INFO 1-A", false)]
    [InlineData("MORGE MAXIME", false)]
    public void IsPlaceholderLine_RecognizesNonInformativeLines(string line, bool expected)
    {
        IcsImportHelper.IsPlaceholderLine(line).Should().Be(expected);
    }
}
