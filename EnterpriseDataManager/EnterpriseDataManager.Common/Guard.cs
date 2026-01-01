namespace EnterpriseDataManager.Common;

using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
public static class Guard
{
    public static void AgainstNullOrEmpty(
        [NotNull] string? value,
        string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrEmpty(value))
            throw new ArgumentException(message, paramName);
    }

    public static void AgainstNullOrWhiteSpace(
        [NotNull] string? value,
        string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(message, paramName);
    }

    public static void AgainstNull<T>(
        [NotNull] T? value,
        string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : class
    {
        if (value is null)
            throw new ArgumentNullException(paramName, message);
    }

    public static void AgainstDefault<T>(
        T value,
        string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null) where T : struct
    {
        if (EqualityComparer<T>.Default.Equals(value, default))
            throw new ArgumentException(message, paramName);
    }

    public static void AgainstNegative(
        long value,
        string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(paramName, value, message);
    }

    public static void AgainstNegativeOrZero(
        long value,
        string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value <= 0)
            throw new ArgumentOutOfRangeException(paramName, value, message);
    }

    public static void AgainstNegative(
        TimeSpan value,
        string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(paramName, value, message);
    }

    public static void AgainstNegativeOrZero(
        TimeSpan value,
        string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(paramName, value, message);
    }

    public static void AgainstOutOfRange(
        int value,
        int min,
        int max,
        string message,
        [CallerArgumentExpression(nameof(value))] string? paramName = null)
    {
        if (value < min || value > max)
            throw new ArgumentOutOfRangeException(paramName, value, message);
    }

    public static void AgainstInvalidOperation(bool condition, string message)
    {
        if (condition)
            throw new InvalidOperationException(message);
    }

    public static void EnsureValidState(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
