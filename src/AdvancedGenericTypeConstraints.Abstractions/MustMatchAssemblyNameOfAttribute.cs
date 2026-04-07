namespace AdvancedGenericTypeConstraints;

/// <summary>
/// Requires a generic type argument or <see cref="Type"/> parameter value to be declared in an assembly whose simple
/// name matches another related type's assembly simple name with an optional prefix and suffix.
/// </summary>
/// <param name="otherTypeParameterName">
/// The name of the related generic type parameter or method parameter whose assembly name is used as the base value.
/// </param>
/// <param name="prefix">An optional prefix added before the related assembly name.</param>
/// <param name="suffix">An optional suffix added after the related assembly name.</param>
[AttributeUsage(AttributeTargets.GenericParameter | AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class MustMatchAssemblyNameOfAttribute(
    string otherTypeParameterName,
    string prefix = "",
    string suffix = "") : Attribute
{
    /// <summary>
    /// Gets the related generic type parameter name.
    /// </summary>
    public string OtherTypeParameterName { get; } = ValidateTypeParameterName(otherTypeParameterName);

    /// <summary>
    /// Gets the required assembly name prefix.
    /// </summary>
    public string Prefix { get; } = prefix ?? string.Empty;

    /// <summary>
    /// Gets the required assembly name suffix.
    /// </summary>
    public string Suffix { get; } = suffix ?? string.Empty;

    /// <summary>
    /// Gets or sets explicitly allowed exception types that bypass the assembly name check.
    /// </summary>
    public Type[] AllowedTypes { get; set; } = [];

    private static string ValidateTypeParameterName(string otherTypeParameterName)
    {
        if (string.IsNullOrWhiteSpace(otherTypeParameterName))
            throw new ArgumentException("The related generic type parameter name must not be null or whitespace.",
                nameof(otherTypeParameterName));

        return otherTypeParameterName;
    }
}
