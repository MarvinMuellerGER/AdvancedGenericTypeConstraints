namespace AdvancedGenericTypeConstraints;

/// <summary>
/// Requires a generic type argument to be annotated with a specific attribute.
/// </summary>
/// <param name="attributeType">The attribute type that must be applied to the supplied type argument.</param>
[AttributeUsage(AttributeTargets.GenericParameter, AllowMultiple = true)]
public sealed class MustHaveAttributeAttribute(Type attributeType) : Attribute
{
    /// <summary>
    /// Gets the required attribute type.
    /// </summary>
    public Type AttributeType { get; } = ValidateAttributeType(attributeType);

    private static Type ValidateAttributeType(Type attributeType)
    {
        if (attributeType is null)
            throw new ArgumentNullException(nameof(attributeType));

        if (!typeof(Attribute).IsAssignableFrom(attributeType))
            throw new ArgumentException("The supplied type must derive from System.Attribute.",
                nameof(attributeType));

        return attributeType;
    }
}
