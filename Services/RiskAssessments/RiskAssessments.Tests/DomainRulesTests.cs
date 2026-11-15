using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using RiskAssessments.Domain.Entities;
using RiskAssessments.Domain.Enums;
using RiskAssessments.Domain.Exceptions;
using RiskAssessments.Domain.ValueObjects;
using RiskAssessments.Infrastructure.Persistence;

namespace RiskAssessments.Tests;

public class DomainRulesTests
{
    [Fact]
    public void Score_above_100_throws_without_touching_a_database()
    {
        var ex = Assert.Throws<DomainException>(() => new RiskAssessment(
            1,
            "Відновлення школи",
            1,
            "Київська міська рада",
            new Auditor("auditor-01", "Марія Коваль"),
            overallRiskScore: 120,
            RiskLevel.Critical));

        Assert.Contains("0 and 100", ex.Message);
    }

    [Fact]
    public void Score_10_with_critical_level_is_rejected()
    {
        Assert.Throws<DomainException>(() => new RiskAssessment(
            1, "Тендер", 1, "Замовник",
            new Auditor("u", "Аудитор"),
            10,
            RiskLevel.Critical));
    }

    [Fact]
    public void Duplicate_flag_is_rejected()
    {
        var assessment = SampleAssessment();
        assessment.AddFlag(new TriggeredFlag("SINGLE_BIDDER", FlagSeverity.Warning, "Один учасник"));

        var ex = Assert.Throws<DomainException>(() =>
            assessment.AddFlag(new TriggeredFlag("SINGLE_BIDDER", FlagSeverity.Info, "Повтор")));

        Assert.Contains("SINGLE_BIDDER", ex.Message);
    }

    [Fact]
    public void Auditors_compare_by_value()
    {
        var left = new Auditor("auditor-01", "Марія Коваль");
        var right = new Auditor("auditor-01", "Марія Коваль");
        Assert.Equal(left, right);
        Assert.NotEqual(left, new Auditor("auditor-02", "Марія Коваль"));
    }

    [Fact]
    public void Money_rejects_a_negative_amount()
    {
        Assert.Throws<DomainException>(() => new Money(-1, "UAH"));
    }

    [Fact]
    public void Bson_round_trip_uses_camelCase_and_hides_private_fields()
    {
        MongoMappings.Register();
        var assessment = SampleAssessment();
        assessment.AddFlag(new TriggeredFlag(
            "SHELL_COMPANY",
            FlagSeverity.Critical,
            "Мінімальний капітал",
            "contractor.registrationDate",
            contractorId: 101,
            companyAgeDays: 320,
            authorizedCapital: 10_000m));
        assessment.AddFlag(new TriggeredFlag(
            "SINGLE_BIDDER",
            FlagSeverity.Warning,
            "Один учасник",
            "bidsCount"));

        var document = assessment.ToBsonDocument();

        Assert.True(document.Contains("_id"));
        Assert.False(document.Contains("id"));
        Assert.False(document.Contains("_triggeredFlags"));
        Assert.False(document.Contains("_auditNoteIds"));
        Assert.Equal("tenderTitle", document.GetElement("tenderTitle").Name);
        Assert.True(document.Contains("triggeredFlags"));
        Assert.True(document.Contains("createdAt"));
        Assert.True(document.Contains("version"));
        Assert.Equal(2, document["triggeredFlags"].AsBsonArray.Count);
        Assert.False(document["triggeredFlags"][1].AsBsonDocument.Contains("companyAgeDays"));
        Assert.False(document["triggeredFlags"][1].AsBsonDocument.Contains("updatedAt"));
        Assert.Equal(320, document["triggeredFlags"][0].AsBsonDocument["companyAgeDays"].ToInt32());
        Assert.Equal("auditor-01", document["auditor"]["userId"].AsString);
        Assert.Equal("expectedBudget", document.GetElement("expectedBudget").Name);

        var restored = BsonSerializer.Deserialize<RiskAssessment>(document);
        Assert.Equal(assessment.TenderTitle, restored.TenderTitle);
        Assert.Equal(assessment.Auditor, restored.Auditor);
        Assert.Equal(2, restored.TriggeredFlags.Count);
        Assert.Equal("SHELL_COMPANY", restored.TriggeredFlags.First().RuleCode);
        Assert.Equal(320, restored.TriggeredFlags.First(f => f.RuleCode == "SHELL_COMPANY").CompanyAgeDays);
    }

    [Fact]
    public void Audit_note_embeds_attachments_as_smallAttachments()
    {
        MongoMappings.Register();
        var note = new AuditNote(
            ObjectId.GenerateNewId(),
            new Auditor("auditor-01", "Марія Коваль"),
            "Запит документів",
            NoteType.Request,
            isPinned: true);
        note.AddAttachment(new Attachment("Запит.pdf", "https://internal.gov.ua/docs/req-001.pdf"));

        var document = note.ToBsonDocument();
        Assert.False(document.Contains("_attachments"));
        Assert.True(document.Contains("smallAttachments"));
        Assert.Equal("auditor-01", document["authorId"].AsString);
        Assert.Equal("Марія Коваль", document["author"]["displayName"].AsString);

        var restored = BsonSerializer.Deserialize<AuditNote>(document);
        Assert.Single(restored.Attachments);
        Assert.Equal(note.Author, restored.Author);
    }

    private static RiskAssessment SampleAssessment() => new(
        2,
        "Ремонт теплотраси — мікрорайон «Залютине»",
        2,
        "Харківська обласна адміністрація",
        new Auditor("auditor-01", "Марія Коваль"),
        72.5,
        RiskLevel.High,
        new Money(12_000_000, "UAH"));
}
