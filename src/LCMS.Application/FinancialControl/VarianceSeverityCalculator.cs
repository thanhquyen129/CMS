using LCMS.Domain.Entities;
using Microsoft.Extensions.Options;

namespace LCMS.Application.FinancialControl;

public interface IVarianceSeverityCalculator
{
    string FromAmount(decimal varianceAmount);
}

public sealed class VarianceSeverityCalculator : IVarianceSeverityCalculator
{
    private readonly FinancialControlOptions _options;

    public VarianceSeverityCalculator(IOptions<FinancialControlOptions> options)
    {
        _options = options.Value;
    }

    public string FromAmount(decimal varianceAmount)
    {
        var abs = Math.Abs(varianceAmount);
        var t = _options.VarianceSeverityThresholds;
        if (abs >= t.Critical)
        {
            return VarianceSeverities.Critical;
        }

        if (abs >= t.High)
        {
            return VarianceSeverities.High;
        }

        if (abs >= t.Medium)
        {
            return VarianceSeverities.Medium;
        }

        return VarianceSeverities.Low;
    }
}
