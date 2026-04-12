using ErrorOr;

namespace RatBot.Domain.Extensions;

public static class ErrorOrExtensions
{
    extension<T>(ErrorOr<T> source)
    {
        /// <summary>
        /// Ensures that the source value satisfies the predicate.
        /// </summary>
        /// <param name="predicate">The predicate function to validate the source value.</param>
        /// <returns>An <see cref="ErrorOr{TValue}" /> containing the validated value or an error if the predicate fails.</returns>
        public ErrorOr<T> Ensure(Func<T, ErrorOr<Success>> predicate) =>
            source.Then(value => predicate(value).Then(_ => value));
    }
}