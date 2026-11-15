using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace RiskAssessments.WebApi.Controllers;

[ApiController]
public abstract class ApiControllerBase(ISender mediator) : ControllerBase
{
    protected ISender Mediator { get; } = mediator;

    protected static int ReadVersion(string? ifMatch)
    {
        if (string.IsNullOrWhiteSpace(ifMatch))
            return -1;

        var value = ifMatch.Trim();
        if (value.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
            value = value[2..].Trim();

        value = value.Trim('"');
        return int.TryParse(value, out var version) ? version : -1;
    }
}
