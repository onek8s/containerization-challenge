namespace FreightRates.Core;

public static class ShipmentValidator
{
    public const int MaxLines = 20;
    public const int MaxLineWeightLbs = 20000;
    public const int MaxShipmentWeightLbs = 20000;

    public static IReadOnlyList<string> Validate(ShipmentRequest shipment, DateOnly today)
    {
        var errors = new List<string>();

        if (!IsZip(shipment.OriginZip))
            errors.Add("Origin ZIP code must be 5 digits.");
        if (!IsZip(shipment.DestinationZip))
            errors.Add("Destination ZIP code must be 5 digits.");

        if (shipment.ShipDate < today.AddDays(-30))
            errors.Add("Ship date cannot be more than 30 days in the past.");
        if (shipment.ShipDate > today.AddDays(180))
            errors.Add("Ship date cannot be more than 180 days in the future.");

        if (shipment.Lines.Count == 0)
            errors.Add("At least one line is required.");
        if (shipment.Lines.Count > MaxLines)
            errors.Add($"A shipment can have at most {MaxLines} lines.");

        for (var i = 0; i < shipment.Lines.Count; i++)
        {
            var line = shipment.Lines[i];
            var n = i + 1;
            if (line.WeightLbs <= 0 || line.WeightLbs > MaxLineWeightLbs)
                errors.Add($"Line {n}: weight must be between 1 and {MaxLineWeightLbs} lbs.");
            if (line.Pieces <= 0)
                errors.Add($"Line {n}: pieces must be at least 1.");
            if (line.FreightClass is { } cls)
            {
                if (!FreightClass.IsValid(cls))
                    errors.Add($"Line {n}: {cls} is not a valid freight class.");
            }
            else if (line.LengthIn is not > 0 || line.WidthIn is not > 0 || line.HeightIn is not > 0)
            {
                errors.Add($"Line {n}: freight class or all dimensions are required.");
            }
        }

        if (shipment.TotalWeightLbs > MaxShipmentWeightLbs)
            errors.Add($"Total weight exceeds {MaxShipmentWeightLbs} lbs; request a volume quote.");

        return errors;
    }

    private static bool IsZip(string? zip) => zip is { Length: 5 } && zip.All(char.IsAsciiDigit);
}
