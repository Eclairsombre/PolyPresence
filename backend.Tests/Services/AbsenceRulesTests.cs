using backend.Models;
using backend.Services;
using FluentAssertions;

namespace backend.Tests.Services;

public class AbsenceRulesTests
{
    [Theory]
    [InlineData(2026, 9, 1, 2026)]
    [InlineData(2026, 8, 31, 2025)]
    [InlineData(2027, 1, 15, 2026)]
    [InlineData(2026, 12, 31, 2026)]
    public void AcademicYearOf_StartsOnSeptemberFirst(int y, int m, int d, int expected)
    {
        AbsenceRules.AcademicYearOf(new DateTime(y, m, d)).Should().Be(expected);
    }

    [Fact]
    public void AcademicYearRange_CoversSeptemberToAugust()
    {
        var (start, end) = AbsenceRules.AcademicYearRange(2026);

        start.Should().Be(new DateTime(2026, 9, 1));
        end.Should().Be(new DateTime(2027, 9, 1));
    }

    [Fact]
    public void DurationMinutes_IsTheSessionLength_AndNeverNegative()
    {
        var start = new DateTime(2026, 10, 5, 8, 0, 0);

        AbsenceRules.DurationMinutes(start, start.AddHours(3)).Should().Be(180);
        AbsenceRules.DurationMinutes(start, start.AddMinutes(-10)).Should().Be(0);
    }

    [Theory]
    [InlineData(GroupType.Language, true)]
    [InlineData(GroupType.Lv1, true)]
    [InlineData(GroupType.Lv2, true)]
    [InlineData(GroupType.Sub, false)]
    [InlineData(GroupType.Promo, false)]
    public void IsLanguageKind_RecognisesLanguageCalendars(GroupType kind, bool expected)
    {
        AbsenceRules.IsLanguageKind(kind).Should().Be(expected);
    }

    [Fact]
    public void ComputeCourseStats_RateCountsOnlyUnjustifiedAbsences()
    {
        // Exemple du cahier des charges : 4 séances signées de 3 h, une absence injustifiée
        // et une justifiée donnent 3 h / 12 h = 25 %.
        var algo = new CourseKey("Algo", "3A", 1);
        var facts = new[]
        {
            new AttendanceFact(1, algo, 180, IsAbsent: true, IsJustified: false),
            new AttendanceFact(1, algo, 180, IsAbsent: true, IsJustified: true),
            new AttendanceFact(1, algo, 180, IsAbsent: false, IsJustified: false),
            new AttendanceFact(1, algo, 180, IsAbsent: false, IsJustified: false),
        };

        var stats = AbsenceRules.ComputeCourseStats(facts).Single();

        stats.SessionCount.Should().Be(4);
        stats.TotalMinutes.Should().Be(720);
        stats.AbsenceCount.Should().Be(2);
        stats.AbsenceMinutes.Should().Be(360);
        stats.JustifiedCount.Should().Be(1);
        stats.UnjustifiedCount.Should().Be(1);
        stats.UnjustifiedMinutes.Should().Be(180);
        stats.Rate.Should().BeApproximately(0.25, 1e-9);
    }

    [Fact]
    public void ComputeCourseStats_GroupsByStudentAndCourse()
    {
        var algo = new CourseKey("Algo", "3A", 1);
        var algo4A = new CourseKey("Algo", "4A", 1);
        var facts = new[]
        {
            new AttendanceFact(1, algo, 120, true, false),
            new AttendanceFact(1, algo4A, 120, false, false),
            new AttendanceFact(2, algo, 120, false, false),
        };

        var stats = AbsenceRules.ComputeCourseStats(facts);

        stats.Should().HaveCount(3);
        stats.Single(s => s.StudentId == 1 && s.Course == algo).Rate.Should().Be(1);
        stats.Single(s => s.StudentId == 1 && s.Course == algo4A).Rate.Should().Be(0);
        stats.Single(s => s.StudentId == 2).AbsenceCount.Should().Be(0);
    }

    [Fact]
    public void ComputeCourseStats_ZeroDuration_GivesZeroRate()
    {
        var course = new CourseKey("Algo", "3A", 1);

        var stats = AbsenceRules.ComputeCourseStats(new[] { new AttendanceFact(1, course, 0, true, false) }).Single();

        stats.Rate.Should().Be(0);
    }

    [Fact]
    public void Summarize_AddsUpTheCourses_AndWeightsTheRatesByDuration()
    {
        var stats = AbsenceRules.ComputeCourseStats(new[]
        {
            new AttendanceFact(1, new CourseKey("Algo", "3A", 1), 180, IsAbsent: true, IsJustified: true),
            new AttendanceFact(1, new CourseKey("Algo", "3A", 1), 180, IsAbsent: false, IsJustified: false),
            new AttendanceFact(1, new CourseKey("Anglais", "3A", 2), 120, IsAbsent: true, IsJustified: false),
        });

        var totals = AbsenceRules.Summarize(stats);

        totals.CourseCount.Should().Be(2);
        totals.SessionCount.Should().Be(3);
        totals.TotalMinutes.Should().Be(480);
        totals.AbsenceCount.Should().Be(2);
        totals.JustifiedCount.Should().Be(1);
        totals.UnjustifiedMinutes.Should().Be(120);
        totals.AbsenceRate.Should().BeApproximately(300.0 / 480, 1e-9);
        totals.UnjustifiedRate.Should().BeApproximately(120.0 / 480, 1e-9);
    }

    [Theory]
    [InlineData("AA", 0, 2)]
    [InlineData("APA", -1, 0)]
    [InlineData("AAPAAA", 3, 3)]      // la dernière série, même si une autre est plus ancienne
    [InlineData("AAAPAA", 4, 2)]
    [InlineData("PAAP", 1, 2)]
    [InlineData("", -1, 0)]
    public void LatestAbsenceStreak_FindsTheLastRunOfConsecutiveAbsences(string sessions, int start, int length)
    {
        var streak = AbsenceRules.LatestAbsenceStreak(sessions.Select(c => c == 'A').ToList(), 2);

        if (start < 0) streak.Should().BeNull();
        else streak.Should().Be((start, length));
    }

    [Fact]
    public void Summarize_NoCourse_GivesZeroRates()
    {
        var totals = AbsenceRules.Summarize(new List<CourseStats>());

        totals.TotalMinutes.Should().Be(0);
        totals.AbsenceRate.Should().Be(0);
        totals.UnjustifiedRate.Should().Be(0);
    }
}
