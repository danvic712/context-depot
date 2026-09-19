namespace ContextDepot.Application.Bootstrap;

internal sealed class SimpleTokenEstimator
{
    public int Estimate(params string?[] values)
    {
        var text = string.Join(' ', values.Where(value => !string.IsNullOrWhiteSpace(value)));
        var ascii = text.Count(character => character <= 0x7f);
        var nonAscii = text.Length - ascii;
        return Math.Max(1, (int)Math.Ceiling(ascii / 4d) + nonAscii);
    }
}
