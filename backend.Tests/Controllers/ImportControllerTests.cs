using backend.Controllers;
using backend.Models;
using backend.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using System.Reflection;

namespace backend.Tests.Controllers;

public class ImportControllerTests
{
    private static ImportController BuildController(backend.Data.ApplicationDbContext db)
    {
        return new ImportController(
            db,
            NullLogger<ImportController>.Instance,
            new ServiceCollection().BuildServiceProvider().GetRequiredService<IServiceScopeFactory>());
    }

    [Fact]
    public async Task ImportIcs_ShouldReturnBadRequest_WhenUrlOrYearMissing()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.ImportIcs(new ImportController.ImportIcsModel
        {
            IcsUrl = "",
            Year = ""
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ImportIcs_ShouldReturnBadRequest_WhenSpecializationNotFound()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var result = await controller.ImportIcs(new ImportController.ImportIcsModel
        {
            IcsUrl = "http://example.com/edt.ics",
            Year = "3A",
            SpecializationId = 999
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ImportIcs_ShouldReturnBadRequest_WhenNoDefaultSpecializationExists()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Name = "GC", Code = "GC" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.ImportIcs(new ImportController.ImportIcsModel
        {
            IcsUrl = "http://example.com/edt.ics",
            Year = "3A",
            SpecializationId = null
        });

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task ImportIcs_ShouldReturnServerError_WhenFetchFails()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Specializations.Add(new Specialization { Id = 1, Name = "Info", Code = "INFO" });
        await db.SaveChangesAsync();

        var controller = BuildController(db);

        var result = await controller.ImportIcs(new ImportController.ImportIcsModel
        {
            IcsUrl = "http://127.0.0.1:1/not-reachable.ics",
            Year = "3A",
            SpecializationId = 1
        });

        result.Should().BeOfType<ObjectResult>().Which.StatusCode.Should().Be(500);
    }

    [Fact]
    public async Task ImportAllIcsLinks_ShouldNotThrow_WhenNoLinks()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var act = async () => await controller.ImportAllIcsLinks(db, NullLogger<ImportController>.Instance);

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public void ParseName_ShouldSplitNameAndFirstname()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var method = typeof(ImportController).GetMethod("ParseName", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        var args = new object[] { "DOE John", "", "" };
        method!.Invoke(controller, args);

        args[1].Should().Be("DOE");
        args[2].Should().Be("John");
    }

    [Fact]
    public void ExtractTargetGroup_ShouldReturnSingleGroup_WhenOneGroupDetected()
    {
        var method = typeof(ImportController).GetMethod("ExtractTargetGroup", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var description = "3A-1 Apprentissage Informatique\nSalle A1";
        var result = method!.Invoke(null, new object[] { description });

        result.Should().Be("3A-1");
    }

    [Fact]
    public void ExtractTargetGroup_ShouldReturnEmpty_WhenMultipleGroupsDetected()
    {
        var method = typeof(ImportController).GetMethod("ExtractTargetGroup", BindingFlags.NonPublic | BindingFlags.Static);
        method.Should().NotBeNull();

        var description = "3A-1 Apprentissage Informatique\n3A-2 Apprentissage Informatique";
        var result = method!.Invoke(null, new object[] { description });

        result.Should().Be("");
    }

    [Fact]
    public void CombineStrings_ShouldDeduplicateAndMergeValues()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var method = typeof(ImportController).GetMethod("CombineStrings", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        var result = method!.Invoke(controller, new object[] { "TP / CM", "CM / TD" });

        result.Should().Be("TP / CM / TD");
    }

    [Fact]
    public void CombineStrings_ShouldReturnNonEmpty_WhenOneSideEmpty()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var method = typeof(ImportController).GetMethod("CombineStrings", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        method!.Invoke(controller, new object[] { "", "TD" }).Should().Be("TD");
        method.Invoke(controller, new object[] { "CM", "" }).Should().Be("CM");
    }

    [Fact]
    public void ApplyBusinessRules_ShouldMergeOverlappingSessions()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var importedType = typeof(ImportController).GetNestedType("ImportedSession", BindingFlags.NonPublic);
        importedType.Should().NotBeNull();

        var listType = typeof(List<>).MakeGenericType(importedType!);
        var sessions = Activator.CreateInstance(listType)!;

        object s1 = Activator.CreateInstance(importedType!)!;
        importedType!.GetProperty("Date")!.SetValue(s1, DateTime.Today);
        importedType.GetProperty("Start")!.SetValue(s1, TimeSpan.FromHours(8));
        importedType.GetProperty("End")!.SetValue(s1, TimeSpan.FromHours(10));
        importedType.GetProperty("Name")!.SetValue(s1, "Math");
        importedType.GetProperty("Room")!.SetValue(s1, "A1");
        importedType.GetProperty("ProfId")!.SetValue(s1, "1");
        importedType.GetProperty("ProfId2")!.SetValue(s1, "");

        object s2 = Activator.CreateInstance(importedType)!;
        importedType.GetProperty("Date")!.SetValue(s2, DateTime.Today);
        importedType.GetProperty("Start")!.SetValue(s2, TimeSpan.FromHours(9));
        importedType.GetProperty("End")!.SetValue(s2, TimeSpan.FromHours(11));
        importedType.GetProperty("Name")!.SetValue(s2, "Physique");
        importedType.GetProperty("Room")!.SetValue(s2, "B2");
        importedType.GetProperty("ProfId")!.SetValue(s2, "2");
        importedType.GetProperty("ProfId2")!.SetValue(s2, "");

        listType.GetMethod("Add")!.Invoke(sessions, new[] { s1 });
        listType.GetMethod("Add")!.Invoke(sessions, new[] { s2 });

        var apply = typeof(ImportController).GetMethod("ApplyBusinessRules", BindingFlags.NonPublic | BindingFlags.Instance);
        apply.Should().NotBeNull();
        var result = apply!.Invoke(controller, new[] { sessions });

        var count = (int)listType.GetProperty("Count")!.GetValue(result)!;
        count.Should().Be(1);

        var merged = listType.GetProperty("Item")!.GetValue(result, new object[] { 0 })!;
        importedType.GetProperty("IsMerged")!.GetValue(merged).Should().Be(true);
        importedType.GetProperty("Start")!.GetValue(merged).Should().Be(TimeSpan.FromHours(8));
        importedType.GetProperty("End")!.GetValue(merged).Should().Be(TimeSpan.FromHours(11));
    }

    [Fact]
    public void ExtractProfessors_ShouldIgnoreNoiseAndParseTwoNames()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var method = typeof(ImportController).GetMethod("ExtractProfessors", BindingFlags.NonPublic | BindingFlags.Instance);
        method.Should().NotBeNull();

        var description = "3A-1 Apprentissage Informatique\nDOE John\nSMITH Jane\n(Exporté le 01/01)\nIngénieur";
        var tuple = ((string p1, string f1, string p2, string f2))method!.Invoke(controller, new object[] { description })!;

        tuple.p1.Should().Be("DOE");
        tuple.f1.Should().Be("John");
        tuple.p2.Should().Be("SMITH");
        tuple.f2.Should().Be("Jane");
    }

    [Fact]
    public void ApplyBusinessRules_ShouldMergeConsecutiveSessions()
    {
        using var db = DbContextHelper.CreateInMemoryDbContext();
        var controller = BuildController(db);

        var importedType = typeof(ImportController).GetNestedType("ImportedSession", BindingFlags.NonPublic)!;
        var listType = typeof(List<>).MakeGenericType(importedType);
        var sessions = Activator.CreateInstance(listType)!;

        var s1 = CreateImported(importedType, DateTime.Today, TimeSpan.FromHours(8), TimeSpan.FromHours(10), "Algo", "A1", "11", "", "3A", "");
        var s2 = CreateImported(importedType, DateTime.Today, TimeSpan.FromHours(10.25), TimeSpan.FromHours(12), "Algo", "A1", "11", "", "3A", "");

        listType.GetMethod("Add")!.Invoke(sessions, new[] { s1 });
        listType.GetMethod("Add")!.Invoke(sessions, new[] { s2 });

        var apply = typeof(ImportController).GetMethod("ApplyBusinessRules", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var result = apply.Invoke(controller, new[] { sessions })!;

        var count = (int)listType.GetProperty("Count")!.GetValue(result)!;
        count.Should().Be(1);

        var merged = listType.GetProperty("Item")!.GetValue(result, new object[] { 0 })!;
        importedType.GetProperty("Start")!.GetValue(merged).Should().Be(TimeSpan.FromHours(8));
        importedType.GetProperty("End")!.GetValue(merged).Should().Be(TimeSpan.FromHours(12));
        importedType.GetProperty("IsMerged")!.GetValue(merged).Should().Be(true);
    }

    [Fact]
    public async Task SyncWithDatabase_ShouldCreateSessionAndAttendances_WhenNewSessionImported()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        db.Users.AddRange(
            new User { Id = 1, StudentNumber = "S1", Name = "A", Firstname = "B", Email = "a@b.fr", Year = "3A", SpecializationId = 1, IsDeleted = false },
            new User { Id = 2, StudentNumber = "S2", Name = "C", Firstname = "D", Email = "c@d.fr", Year = "3A", SpecializationId = 1, IsDeleted = true },
            new User { Id = 3, StudentNumber = "S3", Name = "E", Firstname = "F", Email = "e@f.fr", Year = "3A", SpecializationId = 2, IsDeleted = false });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var importedType = typeof(ImportController).GetNestedType("ImportedSession", BindingFlags.NonPublic)!;
        var listType = typeof(List<>).MakeGenericType(importedType);
        var importedList = Activator.CreateInstance(listType)!;

        var imported = CreateImported(importedType, DateTime.Today, TimeSpan.FromHours(8), TimeSpan.FromHours(10), "Algo", "A1", "11", "", "3A", "3A-1");
        listType.GetMethod("Add")!.Invoke(importedList, new[] { imported });

        var sync = typeof(ImportController).GetMethod("SyncWithDatabase", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)sync.Invoke(controller, new object[] { importedList, "3A", 1 })!;
        await task;

        var session = await db.Sessions.SingleAsync();
        session.Name.Should().Be("Algo");
        session.SpecializationId.Should().Be(1);
        session.TargetGroup.Should().Be("3A-1");

        var attendances = await db.Attendances.ToListAsync();
        attendances.Should().HaveCount(1);
        attendances.Single().StudentId.Should().Be(1);
    }

    [Fact]
    public async Task SyncWithDatabase_ShouldUpdateMatch_AndDeleteMissingSessions()
    {
        await using var db = DbContextHelper.CreateInMemoryDbContext();
        var date = DateTime.Today;
        var start = DateTime.SpecifyKind(date.AddHours(8), DateTimeKind.Unspecified);
        var end = DateTime.SpecifyKind(date.AddHours(10), DateTimeKind.Unspecified);

        db.Sessions.AddRange(
            new Session
            {
                Id = 100,
                Date = date,
                StartTime = start,
                EndTime = end,
                Year = "3A",
                Name = "Old",
                Room = "R1",
                ValidationCode = "1111",
                SpecializationId = 1,
                TargetGroup = "",
                ProfId = "1",
                ProfId2 = ""
            },
            new Session
            {
                Id = 101,
                Date = date,
                StartTime = DateTime.SpecifyKind(date.AddHours(14), DateTimeKind.Unspecified),
                EndTime = DateTime.SpecifyKind(date.AddHours(16), DateTimeKind.Unspecified),
                Year = "3A",
                Name = "ToDelete",
                Room = "R2",
                ValidationCode = "2222",
                SpecializationId = 1,
                TargetGroup = "",
                ProfId = "2",
                ProfId2 = ""
            });
        await db.SaveChangesAsync();

        var controller = BuildController(db);
        var importedType = typeof(ImportController).GetNestedType("ImportedSession", BindingFlags.NonPublic)!;
        var listType = typeof(List<>).MakeGenericType(importedType);
        var importedList = Activator.CreateInstance(listType)!;

        var updatedImported = CreateImported(importedType, date, TimeSpan.FromHours(8), TimeSpan.FromHours(10), "Updated", "R9", "9", "10", "3A", "3A-2", isMerged: true);
        listType.GetMethod("Add")!.Invoke(importedList, new[] { updatedImported });

        var sync = typeof(ImportController).GetMethod("SyncWithDatabase", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var task = (Task)sync.Invoke(controller, new object[] { importedList, "3A", 1 })!;
        await task;

        var sessions = await db.Sessions.OrderBy(s => s.Id).ToListAsync();
        sessions.Should().HaveCount(1);
        sessions[0].Id.Should().Be(100);
        sessions[0].Name.Should().Be("Updated");
        sessions[0].Room.Should().Be("R9");
        sessions[0].ProfId.Should().Be("9");
        sessions[0].ProfId2.Should().Be("10");
        sessions[0].TargetGroup.Should().Be("3A-2");
        sessions[0].IsMerged.Should().BeTrue();
    }

    private static object CreateImported(
        Type importedType,
        DateTime date,
        TimeSpan start,
        TimeSpan end,
        string name,
        string room,
        string profId,
        string profId2,
        string year,
        string targetGroup,
        bool isMerged = false)
    {
        var item = Activator.CreateInstance(importedType)!;
        importedType.GetProperty("Date")!.SetValue(item, date.Date);
        importedType.GetProperty("Start")!.SetValue(item, start);
        importedType.GetProperty("End")!.SetValue(item, end);
        importedType.GetProperty("Name")!.SetValue(item, name);
        importedType.GetProperty("Room")!.SetValue(item, room);
        importedType.GetProperty("ProfId")!.SetValue(item, profId);
        importedType.GetProperty("ProfId2")!.SetValue(item, profId2);
        importedType.GetProperty("Year")!.SetValue(item, year);
        importedType.GetProperty("TargetGroup")!.SetValue(item, targetGroup);
        importedType.GetProperty("IsMerged")!.SetValue(item, isMerged);
        return item;
    }
}
