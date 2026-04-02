namespace OpenGenericConstraints;

/// <summary>
/// Requires a generic type argument to match a given open generic type definition.
/// </summary>
/// <param name="openGenericType">
/// The open generic type definition that must be matched by the supplied type argument,
/// one of its base types, or one of its implemented interfaces.
/// </param>
/// <param name="exactlyOne">
/// When <see langword="true"/>, the supplied type argument must match the configured open generic exactly once.
/// Otherwise, at least one match is required.
/// </param>
[AttributeUsage(AttributeTargets.GenericParameter, AllowMultiple = true)]
public sealed class MustImplementOpenGenericAttribute(Type openGenericType, bool exactlyOne = false) : Attribute
{
    /// <summary>
    /// Gets the required open generic type definition.
    /// </summary>
    public Type OpenGenericType { get; } = ValidateOpenGenericType(openGenericType);

    /// <summary>
    /// Gets a value indicating whether exactly one match is required.
    /// </summary>
    public bool ExactlyOne { get; } = exactlyOne;

    private static Type ValidateOpenGenericType(Type openGenericType)
    {
        if (openGenericType is null) throw new ArgumentNullException(nameof(openGenericType));

        if (!openGenericType.IsGenericTypeDefinition)
            throw new ArgumentException("The supplied type must be an open generic type definition.",
                nameof(openGenericType));

        return openGenericType;
    }
}
