using Microsoft.EntityFrameworkCore;
using SchemaHunter.InfrastructureRegistry.Persistence.Entities;

namespace SchemaHunter.InfrastructureRegistry.Persistence;

/// <summary>
/// Startup seeder for Context 2 — Infrastructure Registry.
///
/// Inserts reference entities that cannot cleanly use EF Core HasData()
/// because they carry a ROWVERSION / concurrency token column.
/// Uses idempotent check-then-insert logic so it is safe to call on every
/// application start.
/// </summary>
public static class DbSeeder
{
    public static async Task SeedAsync(InfrastructureDbContext db)
    {
        // ── ReconstructionProjects ────────────────────────────────────────────
        // asset_code values are the cross-context reference keys used by
        // Context 1 (tender.project_code) and Context 3 (audit.projectReference.assetCode).
        if (!await db.ReconstructionProjects.IgnoreQueryFilters().AnyAsync())
        {
            db.ReconstructionProjects.AddRange(
                new ReconstructionProject
                {
                    AssetCode         = "ROAD-UA-001",
                    Name              = "Reconstruction of M-07 Highway Segment Kyiv–Zhytomyr (km 12–27)",
                    AssetCategoryId   = 1,   // ROAD — seeded via HasData
                    AllocatedBudget   = 148_000_000.00m,
                    Status            = "Active",
                    Region            = "Kyiv Oblast",
                    IsDeleted         = false,
                    CreatedAt         = new DateTime(2024, 1, 10, 0, 0, 0, DateTimeKind.Utc)
                },
                new ReconstructionProject
                {
                    AssetCode         = "BLDG-UA-002",
                    Name              = "Restoration of School No. 14, Kharkiv (Buildings A & B)",
                    AssetCategoryId   = 2,   // BLDG
                    AllocatedBudget   = 37_500_000.00m,
                    Status            = "Active",
                    Region            = "Kharkiv Oblast",
                    IsDeleted         = false,
                    CreatedAt         = new DateTime(2024, 2, 1, 0, 0, 0, DateTimeKind.Utc)
                },
                new ReconstructionProject
                {
                    AssetCode         = "BRDG-UA-003",
                    Name              = "Capital Repair of Bridge over Inhulets River on Route H-14",
                    AssetCategoryId   = 3,   // BRDG
                    AllocatedBudget   = 92_000_000.00m,
                    Status            = "Planning",
                    Region            = "Kherson Oblast",
                    IsDeleted         = false,
                    CreatedAt         = new DateTime(2024, 3, 5, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            await db.SaveChangesAsync();
        }

        // ── ProjectPassports (1:1 with first two projects) ───────────────────
        if (!await db.ProjectPassports.AnyAsync())
        {
            var road  = await db.ReconstructionProjects.FirstAsync(p => p.AssetCode == "ROAD-UA-001");
            var bldg  = await db.ReconstructionProjects.FirstAsync(p => p.AssetCode == "BLDG-UA-002");

            db.ProjectPassports.AddRange(
                new ProjectPassport
                {
                    ProjectId                = road.Id,
                    EdessrNumber             = "EDESSR-2024-KY-00142",
                    TechnicalPermitNumber    = "TP-M07-2024-001",
                    GpsLatitude              = 50.450100m,
                    GpsLongitude             = 30.523400m,
                    BuiltAreaSqm             = null,      // road — N/A
                    ConstructionStartDate    = new DateOnly(2024, 4, 1),
                    EstimatedCompletionDate  = new DateOnly(2024, 10, 31),
                    ActualCompletionDate     = null,
                    CreatedAt                = new DateTime(2024, 1, 12, 0, 0, 0, DateTimeKind.Utc)
                },
                new ProjectPassport
                {
                    ProjectId                = bldg.Id,
                    EdessrNumber             = "EDESSR-2024-KH-00089",
                    TechnicalPermitNumber    = "TP-SCHOOL14-2024-002",
                    GpsLatitude              = 49.992600m,
                    GpsLongitude             = 36.231900m,
                    BuiltAreaSqm             = 4200.00m,
                    ConstructionStartDate    = new DateOnly(2024, 5, 15),
                    EstimatedCompletionDate  = new DateOnly(2024, 12, 15),
                    ActualCompletionDate     = null,
                    CreatedAt                = new DateTime(2024, 2, 3, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            await db.SaveChangesAsync();
        }

        // ── FundingSources (1:N with projects) ───────────────────────────────
        if (!await db.FundingSources.IgnoreQueryFilters().AnyAsync())
        {
            var road = await db.ReconstructionProjects.FirstAsync(p => p.AssetCode == "ROAD-UA-001");
            var bldg = await db.ReconstructionProjects.FirstAsync(p => p.AssetCode == "BLDG-UA-002");

            db.FundingSources.AddRange(
                // ROAD-UA-001: EU grant + national budget split
                new FundingSource
                {
                    ProjectId             = road.Id,
                    FunderName            = "European Union — Ukraine Facility",
                    FundType              = "GrantEU",
                    AllocatedAmount       = 100_000_000.00m,
                    CurrencyCode          = "UAH",
                    GrantAgreementNumber  = "EU-UF-2024-UA-0031",
                    IsDeleted             = false,
                    CreatedAt             = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc)
                },
                new FundingSource
                {
                    ProjectId             = road.Id,
                    FunderName            = "State Road Fund of Ukraine",
                    FundType              = "NationalBudget",
                    AllocatedAmount       = 48_000_000.00m,
                    CurrencyCode          = "UAH",
                    GrantAgreementNumber  = null,
                    IsDeleted             = false,
                    CreatedAt             = new DateTime(2024, 1, 15, 0, 0, 0, DateTimeKind.Utc)
                },
                // BLDG-UA-002: World Bank + municipal co-financing
                new FundingSource
                {
                    ProjectId             = bldg.Id,
                    FunderName            = "World Bank — DREAM Programme",
                    FundType              = "WorldBank",
                    AllocatedAmount       = 30_000_000.00m,
                    CurrencyCode          = "UAH",
                    GrantAgreementNumber  = "WB-DREAM-2024-UA-007",
                    IsDeleted             = false,
                    CreatedAt             = new DateTime(2024, 2, 5, 0, 0, 0, DateTimeKind.Utc)
                },
                new FundingSource
                {
                    ProjectId             = bldg.Id,
                    FunderName            = "Kharkiv City Council",
                    FundType              = "Municipal",
                    AllocatedAmount       = 7_500_000.00m,
                    CurrencyCode          = "UAH",
                    GrantAgreementNumber  = null,
                    IsDeleted             = false,
                    CreatedAt             = new DateTime(2024, 2, 5, 0, 0, 0, DateTimeKind.Utc)
                }
            );

            await db.SaveChangesAsync();
        }

        // ── ProjectClassificationTags (M:N join) ─────────────────────────────
        if (!await db.ProjectClassificationTags.AnyAsync())
        {
            var road = await db.ReconstructionProjects.FirstAsync(p => p.AssetCode == "ROAD-UA-001");
            var bldg = await db.ReconstructionProjects.FirstAsync(p => p.AssetCode == "BLDG-UA-002");
            var brdg = await db.ReconstructionProjects.FirstAsync(p => p.AssetCode == "BRDG-UA-003");

            var now = DateTime.UtcNow;
            db.ProjectClassificationTags.AddRange(
                new ProjectClassificationTag { ProjectId = road.Id, TagId = 1, AssignedAt = now }, // PRIORITY
                new ProjectClassificationTag { ProjectId = road.Id, TagId = 2, AssignedAt = now }, // DONOR_FUNDED
                new ProjectClassificationTag { ProjectId = bldg.Id, TagId = 1, AssignedAt = now }, // PRIORITY
                new ProjectClassificationTag { ProjectId = bldg.Id, TagId = 4, AssignedAt = now }, // CRITICAL_INFRA
                new ProjectClassificationTag { ProjectId = brdg.Id, TagId = 2, AssignedAt = now }  // DONOR_FUNDED
            );

            await db.SaveChangesAsync();
        }
    }
}
