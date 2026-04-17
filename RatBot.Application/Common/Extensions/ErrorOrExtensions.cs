namespace RatBot.Application.Common.Extensions;

public static class ErrorOrExtensions
{
    /// <summary>
    ///     Ensures that the source value satisfies the predicate.
    /// </summary>
    /// <param name="source">The source error-or value.</param>
    /// <param name="predicate">The predicate function to validate the source value.</param>
    /// <returns>An <see cref="ErrorOr{TValue}" /> containing the validated value or an error if the predicate fails.</returns>
    public static ErrorOr<T> Ensure<T>(this ErrorOr<T> source, Func<T, ErrorOr<Success>> predicate) =>
        source.Then(value => predicate(value).Then(_ => value));
}