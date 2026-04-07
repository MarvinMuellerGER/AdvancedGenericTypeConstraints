namespace AdvancedGenericTypeConstraints;

/// <summary>
/// Requires a <see cref="Type"/> parameter value to represent a reference type.
/// </summary>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class MustBeReferenceTypeAttribute : Attribute
{
}
