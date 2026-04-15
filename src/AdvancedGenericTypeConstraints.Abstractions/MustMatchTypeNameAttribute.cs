namespace AdvancedGenericTypeConstraints;

/// <summary>
/// Requires a generic type argument name to match a configured prefix and/or suffix.
/// </summary>
/// <param name="prefix">An optional required type name prefix.</param>
/// <param name="suffix">An optional required type name suffix.</param>
[AttributeUsage(AttributeTargets.GenericParameter, AllowMultiple = true)]
public sealed class MustMatchTypeNameAttribute(string prefix = "", string suffix = "") : Attribute
{
    /// <summary>
    /// Gets the required type name prefix.
    /// </summary>
    public string Prefix { get; } = prefix;

    /// <summary>
    /// Gets the required type name suffix.
    /// </summary>
    public string Suffix { get; } = suffix;
}
