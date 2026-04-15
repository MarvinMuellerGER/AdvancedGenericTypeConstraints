using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal readonly struct MustImplementConstraint(INamedTypeSymbol openGenericType, bool exactlyOne)
{
    public INamedTypeSymbol OpenGenericType { get; } = openGenericType;

    public bool ExactlyOne { get; } = exactlyOne;
}

internal readonly struct AssemblyNameConstraint(
    string otherTypeParameterName,
    string prefix,
    string suffix,
    ImmutableArray<INamedTypeSymbol> allowedTypes)
{
    public string OtherTypeParameterName { get; } = otherTypeParameterName;

    public string Prefix { get; } = prefix;

    public string Suffix { get; } = suffix;

    public ImmutableArray<INamedTypeSymbol> AllowedTypes { get; } = allowedTypes;
}

internal readonly struct AssignableToConstraint(string otherParameterName)
{
    public string OtherParameterName { get; } = otherParameterName;
}

internal readonly struct TypeNameConstraint(string prefix, string suffix)
{
    public string Prefix { get; } = prefix;

    public string Suffix { get; } = suffix;
}
