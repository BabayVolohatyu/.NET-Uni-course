using RiskAssessments.Domain.Common;
using RiskAssessments.Domain.Exceptions;

namespace RiskAssessments.Domain.ValueObjects;

/// <summary>
/// Expected tender budget captured at assessment time (UAH in this domain).
/// A snapshot — the Bidding context remains the owner of the live budget.
/// </summary>
public sealed class Money : ValueObject
{
    public decimal Amount { get; }

    public string Currency { get; }

    public Money(decimal amount, string currency)
    {
        if (amount < 0)
            throw new DomainException("Budget amount cannot be negative.");

        if (string.IsNullOrWhiteSpace(currency) || currency.Trim().Length != 3)
            throw new DomainException("Currency must be a 3-letter ISO code (UAH).");

        Amount = amount;
        Currency = currency.Trim().ToUpperInvariant();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Amount;
        yield return Currency;
    }
}
