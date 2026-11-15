using MongoDB.Bson;
using MongoDB.Driver;
using RiskAssessments.Domain.Entities;
using RiskAssessments.Domain.Enums;
using RiskAssessments.Domain.ValueObjects;
using RiskAssessments.Infrastructure.Persistence;

namespace RiskAssessments.Infrastructure.Seeding;

public sealed class AuditNoteSeeder(MongoDbContext context) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await context.AuditNotes.EstimatedDocumentCountAsync(cancellationToken: ct) > 0)
            return;

        var assessment = await context.RiskAssessments
            .Find(a => a.TenderId == 2)
            .FirstOrDefaultAsync(ct);

        if (assessment is null)
            return;

        var discussionId = ObjectId.GenerateNewId();
        await context.Discussions.InsertOneAsync(new BsonDocument
        {
            { "_id", discussionId },
            { "topic", "Верифікація одноосібного учасника тендеру UA-2026-01-002" },
            { "messages", new BsonArray
                {
                    new BsonDocument
                    {
                        { "authorId", "auditor-01" },
                        { "authorName", "Марія Коваль" },
                        { "text", "Тендер отримав лише одну пропозицію при очікуваній вартості 12 млн грн. Потребує перевірки." },
                        { "createdAt", DateTime.UtcNow }
                    },
                    new BsonDocument
                    {
                        { "authorId", "analyst-02" },
                        { "authorName", "Іван Шевченко" },
                        { "text", "Перевірено: підрядник ТОВ «БудМайстер» є єдиним ліцензованим виконавцем у регіоні для цього CPV-коду." },
                        { "createdAt", DateTime.UtcNow }
                    }
                }
            },
            { "createdAt", DateTime.UtcNow }
        }, cancellationToken: ct);

        var request = new AuditNote(
            assessment.Id,
            new Auditor("auditor-01", "Марія Коваль"),
            "Відкрито перевірку. Запит документів надіслано підряднику ТОВ «БудМайстер».",
            NoteType.Request,
            isPinned: true);
        request.AddAttachment(new Attachment(
            "Запит_документів.pdf",
            "https://internal.gov.ua/docs/req-001.pdf"));

        var observation = new AuditNote(
            assessment.Id,
            new Auditor("analyst-02", "Іван Шевченко"),
            "Аналіз підтверджує одноосібне подання. Відкрито обговорення для опрацювання рішення.",
            NoteType.Observation);
        observation.AddAttachment(new Attachment(
            "Аналіз_конкуренції.xlsx",
            "https://internal.gov.ua/docs/analysis-001.xlsx"));
        observation.AttachDiscussion(discussionId);

        await context.AuditNotes.InsertManyAsync([request, observation], cancellationToken: ct);

        await context.Discussions.UpdateOneAsync(
            Builders<BsonDocument>.Filter.Eq("_id", discussionId),
            Builders<BsonDocument>.Update.Set("auditNoteId", observation.Id),
            cancellationToken: ct);

        await context.RiskAssessments.UpdateOneAsync(
            Builders<RiskAssessment>.Filter.Eq(a => a.Id, assessment.Id),
            Builders<RiskAssessment>.Update
                .AddToSetEach("auditNoteIds", new[] { request.Id, observation.Id })
                .Set(a => a.UpdatedAt, DateTime.UtcNow),
            cancellationToken: ct);
    }
}
