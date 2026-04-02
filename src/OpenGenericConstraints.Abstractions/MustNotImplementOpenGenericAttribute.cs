namespace OpenGenericConstraints;

[AttributeUsage(AttributeTargets.GenericParameter, AllowMultiple = true)]
public sealed class MustNotImplementOpenGenericAttribute : Attribute
{
    public MustNotImplementOpenGenericAttribute(Type openGenericType)
    {
        OpenGenericType = ValidateOpenGenericType(openGenericType);
    }

    public Type OpenGenericType { get; }

    private static Type ValidateOpenGenericType(Type openGenericType)
    {
        if (openGenericType is null)
        {
            throw new ArgumentNullException(nameof(openGenericType));
        }

        if (!openGenericType.IsGenericTypeDefinition)
        {
            throw new ArgumentException("The supplied type must be an open generic type definition.", nameof(openGenericType));
        }

        return openGenericType;
    }
}
