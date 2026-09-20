namespace SchemaHunter.Procurement.Domain.Commands;

/// <summary>Input model for creating a new supplier.</summary>
public record CreateSupplierCommand(
    string   EdrpouCode,
    string   LegalName,
    string   TaxStatus,
    DateOnly RegistrationDate);
