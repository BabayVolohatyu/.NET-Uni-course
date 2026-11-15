using Catalog.Domain.Models;
using Microsoft.EntityFrameworkCore;

namespace Catalog.Dal.Seeding;

/// <summary>
/// Programmatic idempotent seeder for Contractor, ContractorDetail, ContractorDocument,
/// and ContractorWorkCategory records.
///
/// Idempotency guarantee: the guard check "if (await db.Contractors.AnyAsync())" ensures
/// a repeated call (e.g. from CI/CD restart) produces no duplicate rows.
///
/// WorkCategory reference data uses HasData (static/model seeding) — see WorkCategoryConfiguration.
/// That data is handled entirely by migrations and does not appear here.
///
/// Execution order in Program.cs:
///   1. db.Database.MigrateAsync()     — creates schema + applies HasData
///   2. DbInitializer.SeedAsync(db)    — adds contractor records (skipped if already seeded)
/// </summary>
public static class DbInitializer
{
    public static async Task SeedAsync(CatalogDbContext db, CancellationToken ct = default)
    {
        // Guard — idempotency: skip if already seeded
        if (await db.Contractors.AnyAsync(ct)) return;

        // ── Seed Contractors ─────────────────────────────────────────────────────
        var budbud = new Contractor(
            name: "ТОВ \"Будбуд Україна\"",
            edrpou: "12345678",
            authorizedCapital: 1_500_000m,
            registrationDate: new DateTime(2015, 3, 15, 0, 0, 0, DateTimeKind.Utc));

        var megastroy = new Contractor(
            name: "ПрАТ \"Мегабуд\"",
            edrpou: "87654321",
            authorizedCapital: 5_000_000m,
            registrationDate: new DateTime(2010, 7, 1, 0, 0, 0, DateTimeKind.Utc));

        var novabud = new Contractor(
            name: "ФОП Коваленко Роман Іванович",
            edrpou: "11223344",
            authorizedCapital: 250_000m,
            registrationDate: new DateTime(2020, 1, 20, 0, 0, 0, DateTimeKind.Utc));

        db.Contractors.AddRange(budbud, megastroy, novabud);
        await db.SaveChangesAsync(ct);

        // ── Seed ContractorDetails (1:1) ─────────────────────────────────────────
        db.ContractorDetails.AddRange(
            new ContractorDetail
            {
                ContractorId = budbud.Id,
                LegalAddress = "вул. Будівельна, 1, м. Київ, 01001",
                DirectorName = "Петренко Сергій Миколайович",
                ContactEmail = "info@budbud.ua",
                ContactPhone = "+380441234567"
            },
            new ContractorDetail
            {
                ContractorId = megastroy.Id,
                LegalAddress = "пр. Перемоги, 42, м. Харків, 61001",
                DirectorName = "Іванченко Олег Петрович",
                ContactEmail = "office@megabud.ua",
                ContactPhone = "+380577654321"
            },
            new ContractorDetail
            {
                ContractorId = novabud.Id,
                LegalAddress = "вул. Соборна, 15, м. Дніпро, 49000",
                DirectorName = "Коваленко Роман Іванович",
                ContactEmail = "kovalenko.roman@gmail.com",
                ContactPhone = null
            }
        );

        // ── Seed ContractorDocuments (1:N) ───────────────────────────────────────
        db.ContractorDocuments.AddRange(
            new ContractorDocument
            {
                ContractorId = budbud.Id,
                DocumentType = "LicenseCC1",
                DocumentNumber = "АЕ-123456",
                IssuedAt = new DateTime(2019, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                ExpiresAt = new DateTime(2024, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ContractorDocument
            {
                ContractorId = budbud.Id,
                DocumentType = "ISO9001",
                DocumentNumber = "ISO-2019-00123",
                IssuedAt = new DateTime(2019, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                ExpiresAt = new DateTime(2025, 1, 15, 0, 0, 0, DateTimeKind.Utc)
            },
            new ContractorDocument
            {
                ContractorId = megastroy.Id,
                DocumentType = "LicenseCC1",
                DocumentNumber = "АЕ-654321",
                IssuedAt = new DateTime(2015, 3, 20, 0, 0, 0, DateTimeKind.Utc),
                ExpiresAt = null    // unlimited licence
            }
        );

        // ── Seed ContractorWorkCategories (M:N) ──────────────────────────────────
        // WorkCategory Ids 1-5 are from HasData in WorkCategoryConfiguration
        db.ContractorWorkCategories.AddRange(
            new ContractorWorkCategory
            {
                ContractorId = budbud.Id,
                CategoryId = 1,     // ROAD
                QualificationDate = new DateTime(2019, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ContractorWorkCategory
            {
                ContractorId = budbud.Id,
                CategoryId = 2,     // BLDG
                QualificationDate = new DateTime(2019, 6, 1, 0, 0, 0, DateTimeKind.Utc)
            },
            new ContractorWorkCategory
            {
                ContractorId = megastroy.Id,
                CategoryId = 2,     // BLDG
                QualificationDate = new DateTime(2015, 3, 20, 0, 0, 0, DateTimeKind.Utc)
            },
            new ContractorWorkCategory
            {
                ContractorId = megastroy.Id,
                CategoryId = 3,     // ELEC
                QualificationDate = new DateTime(2018, 5, 10, 0, 0, 0, DateTimeKind.Utc)
            },
            new ContractorWorkCategory
            {
                ContractorId = novabud.Id,
                CategoryId = 4,     // PIPE
                QualificationDate = new DateTime(2021, 2, 1, 0, 0, 0, DateTimeKind.Utc)
            }
        );

        await db.SaveChangesAsync(ct);
    }
}
