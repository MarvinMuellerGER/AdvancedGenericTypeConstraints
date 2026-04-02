namespace OpenGenericConstraints;

[AttributeUsage(AttributeTargets.GenericParameter, AllowMultiple = true)]
public sealed class MustImplementOpenGenericAttribute : Attribute
{
    public MustImplementOpenGenericAttribute(Type openGenericType)
    {
        OpenGenericType = openGenericType ?? throw new ArgumentNullException(nameof(openGenericType));

        if (!openGenericType.IsGenericTypeDefinition)
        {
            throw new ArgumentException("The supplied type must be an open generic type definition.", nameof(openGenericType));
        }

        if (!openGenericType.IsInterface)
        {
            throw new ArgumentException("The supplied type must be an interface.", nameof(openGenericType));
        }
    }

    public Type OpenGenericType { get; }
}
