using System.Globalization;

namespace ToucanTally.Formatting;

/// <summary>
/// This class is responsible for making numbers pretty,
/// or ugly when someone wants to abbreviate them with k/m.
/// </summary>
internal static class NumberFormatter
{
    public static string Format(uint amount, NumberFormat format)
    {
        if (format == NumberFormat.Full || amount < 1000)
        {
            return amount.ToString("N0", CultureInfo.InvariantCulture);
        }

        if (amount < 1_000_000)
        {
            return Short(amount / 1000f, "k");
        }

        return Short(amount / 1_000_000f, "m");
    }

    private static string Short(float value, string suffix)
    {
        var digits = value < 10 ? 1 : 0;
        return value.ToString($"F{digits}", CultureInfo.InvariantCulture) + suffix;
    }
}
