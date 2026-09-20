namespace SchemaHunter.InfrastructureRegistry.Persistence.Entities;

public class ProjectPassport
{
    public long Id { get; set; }
    public long ProjectId { get; set; }

    /// <summary>
    /// National building registry tracking number (EDESSB / EDESSR).
    /// Nullable — assigned after state registration.
    /// </summary>
    public string? EdessrNumber { get; set; }

    public string? TechnicalPermitNumber { get; set; }
    public decimal? GpsLatitude { get; set; }
    public decimal? GpsLongitude { get; set; }

    /// <summary>Total built area in square metres.</summary>
    public decimal? BuiltAreaSqm { get; set; }

    public DateOnly? ConstructionStartDate { get; set; }
    public DateOnly? EstimatedCompletionDate { get; set; }
    public DateOnly? ActualCompletionDate { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>Optimistic concurrency token — maps to SQL Server ROWVERSION.</summary>
    public byte[] RowVersion { get; set; } = null!;

    public ReconstructionProject Project { get; set; } = null!;
}
