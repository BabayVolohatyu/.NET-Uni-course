using MongoDB.Bson;
using MongoDB.Driver;
using RiskAssessments.Infrastructure.Persistence;

namespace RiskAssessments.Infrastructure.Seeding;

/// <summary>
/// Flexible-schema catalog: each rule document has a different parameter shape.
/// </summary>
public sealed class AnomalyRuleSeeder(MongoDbContext context) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await context.AnomalyRules.EstimatedDocumentCountAsync(cancellationToken: ct) > 0)
            return;

        var now = DateTime.UtcNow;
        var rules = new[]
        {
            new BsonDocument
            {
                { "code", "SINGLE_BIDDER" },
                { "name", "Єдиний учасник" },
                { "severity", "Warning" },
                { "description", "Тендер отримав лише одну пропозицію, що може свідчити про штучне обмеження конкуренції." },
                { "isActive", true },
                { "createdAt", now }
            },
            new BsonDocument
            {
                { "code", "PRICE_DUMPING" },
                { "name", "Демпінгова ціна" },
                { "severity", "Critical" },
                { "description", "Запропонована ціна менша за порогове значення відносно очікуваної вартості." },
                { "isActive", true },
                { "maxPriceRatioPct", 50 },
                { "createdAt", now }
            },
            new BsonDocument
            {
                { "code", "SHELL_COMPANY" },
                { "name", "Ознаки підставної компанії" },
                { "severity", "Critical" },
                { "description", "Компанія зареєстрована нещодавно та/або має мінімальний статутний капітал." },
                { "isActive", true },
                { "maxCompanyAgeDays", 365 },
                { "maxAuthorizedCapital", 10000 },
                { "createdAt", now }
            },
            new BsonDocument
            {
                { "code", "RUSHED_DEADLINE" },
                { "name", "Скорочені терміни подачі" },
                { "severity", "Warning" },
                { "description", "Строк подачі пропозицій є підозріло коротким для тендеру такого масштабу." },
                { "isActive", true },
                { "minDeadlineDays", 7 },
                { "budgetThreshold", 1000000 },
                { "createdAt", now }
            }
        };

        await context.AnomalyRules.InsertManyAsync(rules, cancellationToken: ct);
    }
}
