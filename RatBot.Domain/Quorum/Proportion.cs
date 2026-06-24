namespace RatBot.Domain.Quorum;

public readonly record struct Proportion
{
    private Proportion(double value)
    {
        Value = value;
    }

    public double Value { get; }

    public static ErrorOr<Proportion> Create(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return Error.Validation(description: "Quorum proportion must be a finite number.");

        if (value is <= 0 or > 1)
            return Error.Validation(description: "Quorum proportion must be greater than 0 and at most 1.");

        return new Proportion(value);
    }
}
