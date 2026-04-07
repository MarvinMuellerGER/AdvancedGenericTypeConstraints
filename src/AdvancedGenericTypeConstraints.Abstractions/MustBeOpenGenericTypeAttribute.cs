namespace AdvancedGenericTypeConstraints;

/// <summary>
/// Requires a <see cref="Type"/> parameter value to represent an open generic type definition.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class MustBeOpenGenericTypeAttribute : Attribute;
