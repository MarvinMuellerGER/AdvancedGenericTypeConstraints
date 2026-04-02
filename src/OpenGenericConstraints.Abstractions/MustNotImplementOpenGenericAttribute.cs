namespace OpenGenericConstraints;

/// <summary>
/// Forbids a generic type argument from matching a given open generic type definition.
/// </summary>
/// <param name="openGenericType">
/// The open generic type definition that must not appear on the supplied type argument,
/// any of its base types, or any of its implemented interfaces.
/// </param>
[AttributeUsage(AttributeTargets.GenericParameter, AllowMultiple = true)]
public sealed class MustNotImplementOpenGenericAttribute(Type openGenericType) : Attribute
{
    /// <summary>
    /// Gets the forbidden open generic type definition.
    /// </summary>
    public Type OpenGenericType { get; } = ValidateOpenGenericType(openGenericType);

    private static Type ValidateOpenGenericType(Type openGenericType)
    {
        if (openGenericType is null)
            throw new ArgumentNullException(nameof(openGenericType));

        if (!openGenericType.IsGenericTypeDefinition)
            throw new ArgumentException("The supplied type must be an open generic type definition.",
                nameof(openGenericType));

        return openGenericType;
    }
}
