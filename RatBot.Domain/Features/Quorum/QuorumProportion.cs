namespace RatBot.Domain.Features.Quorum;

public readonly record struct QuorumProportion
{
    private QuorumProportion(decimal value) => Value = value;

    public decimal Value { get; }

    public static ErrorOr<QuorumProportion> Create(decimal value) =>
        value is > 0 and <= 1
            ? new QuorumProportion(value)
            : Error.Validation(description: "Quorum proportion must be greater than 0 and at most 1.");
}
