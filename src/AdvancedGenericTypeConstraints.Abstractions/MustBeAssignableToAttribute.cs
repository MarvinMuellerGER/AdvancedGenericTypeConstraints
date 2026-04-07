namespace AdvancedGenericTypeConstraints;

/// <summary>
/// Requires a <see cref="Type"/> parameter value to be assignable to another related <see cref="Type"/> parameter.
/// </summary>
/// <param name="otherParameterName">
/// The name of the related method parameter whose represented type must be assignable from the annotated parameter.
/// </param>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class MustBeAssignableToAttribute(string otherParameterName) : Attribute
{
    /// <summary>
    /// Gets the related method parameter name.
    /// </summary>
    public string OtherParameterName { get; } = ValidateParameterName(otherParameterName);

    private static string ValidateParameterName(string otherParameterName)
    {
        if (string.IsNullOrWhiteSpace(otherParameterName))
            throw new ArgumentException("The related parameter name must not be null or whitespace.",
                nameof(otherParameterName));

        return otherParameterName;
    }
}
