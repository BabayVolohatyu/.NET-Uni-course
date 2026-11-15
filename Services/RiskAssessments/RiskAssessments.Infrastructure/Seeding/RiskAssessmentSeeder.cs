using RiskAssessments.Domain.Entities;
using RiskAssessments.Domain.Enums;
using RiskAssessments.Domain.ValueObjects;
using RiskAssessments.Infrastructure.Persistence;

namespace RiskAssessments.Infrastructure.Seeding;

public sealed class RiskAssessmentSeeder(MongoDbContext context) : IDataSeeder
{
    public async Task SeedAsync(CancellationToken ct = default)
    {
        if (await context.RiskAssessments.EstimatedDocumentCountAsync(cancellationToken: ct) > 0)
            return;

        var system = new Auditor("system-auto", "Система");

        var school = new RiskAssessment(
            tenderId: 1,
            tenderTitle: "Відновлення школи №15 — покрівля та фасад",
            procuringEntityId: 1,
            procuringEntityName: "Київська міська рада (Відділ відновлення)",
            auditor: system,
            overallRiskScore: 25,
            riskLevel: RiskLevel.Low,
            expectedBudget: new Money(8_500_000, "UAH"));

        var heating = new RiskAssessment(
            tenderId: 2,
            tenderTitle: "Ремонт теплотраси — мікрорайон «Залютине»",
            procuringEntityId: 2,
            procuringEntityName: "Харківська обласна адміністрація",
            auditor: system,
            overallRiskScore: 72.5,
            riskLevel: RiskLevel.High,
            expectedBudget: new Money(12_000_000, "UAH"));

        heating.AddFlag(new TriggeredFlag(
            "SINGLE_BIDDER",
            FlagSeverity.Warning,
            "Тендер отримав лише одну пропозицію при очікуваній вартості 12 000 000 грн.",
            affectedField: "bidsCount"));

        heating.AddFlag(new TriggeredFlag(
            "SHELL_COMPANY",
            FlagSeverity.Critical,
            "Єдиний учасник зареєстрований менше 2 років тому з мінімальним статутним капіталом.",
            affectedField: "contractor.registrationDate",
            contractorId: 101,
            companyAgeDays: 320,
            authorizedCapital: 10_000m));

        await context.RiskAssessments.InsertManyAsync([school, heating], cancellationToken: ct);
    }
}
