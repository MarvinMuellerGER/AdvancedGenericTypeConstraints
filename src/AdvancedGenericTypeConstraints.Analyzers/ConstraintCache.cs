using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal sealed class ConstraintCache(Compilation compilation, ConstraintAttributeSymbols symbols)
{
    private readonly ConcurrentDictionary<IParameterSymbol, ParameterConstraintData> _parameterConstraints =
        new(SymbolEqualityComparer.Default);

    private readonly INamedTypeSymbol? _systemTypeSymbol = compilation.GetTypeByMetadataName("System.Type");

    private readonly ConcurrentDictionary<ITypeParameterSymbol, TypeParameterConstraintData> _typeParameterConstraints =
        new(SymbolEqualityComparer.Default);

    public TypeParameterConstraintData GetTypeParameterConstraints(ITypeParameterSymbol typeParameter) =>
        _typeParameterConstraints.GetOrAdd(typeParameter, CreateTypeParameterConstraintData);

    public ParameterConstraintData GetParameterConstraints(IParameterSymbol parameter) =>
        _parameterConstraints.GetOrAdd(parameter, CreateParameterConstraintData);

    public bool IsSystemType(ITypeSymbol type) =>
        _systemTypeSymbol is not null &&
        SymbolEqualityComparer.Default.Equals(type, _systemTypeSymbol);

    private TypeParameterConstraintData CreateTypeParameterConstraintData(ITypeParameterSymbol typeParameter) =>
        new(
            ConstraintReaders.GetMustImplementConstraints(typeParameter, symbols.MustImplementAttribute),
            ConstraintReaders.GetMustNotImplementConstraints(typeParameter, symbols.MustNotImplementAttribute),
            ConstraintReaders.GetMustHaveAttributeConstraints(typeParameter, symbols.MustHaveAttribute),
            ConstraintReaders.GetAssemblyNameConstraints(typeParameter, symbols.MustMatchAssemblyNameOfAttribute),
            ConstraintReaders.GetTypeNameConstraints(typeParameter, symbols.MustMatchTypeNameAttribute));

    private ParameterConstraintData CreateParameterConstraintData(IParameterSymbol parameter)
    {
        var attributes = parameter.GetAttributes();

        return new ParameterConstraintData(
            ConstraintReaders.GetAssemblyNameConstraints(attributes, symbols.MustMatchAssemblyNameOfAttribute),
            ConstraintReaders.GetAssignableToConstraints(attributes, symbols.MustBeAssignableToAttribute),
            HasAttribute(attributes, symbols.MustBeOpenGenericTypeAttribute),
            HasAttribute(attributes, symbols.MustBeReferenceTypeAttribute));
    }

    private static bool HasAttribute(ImmutableArray<AttributeData> attributes, INamedTypeSymbol? attributeSymbol)
    {
        return attributeSymbol is not null && attributes.Any(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol));
    }
}

internal readonly struct TypeParameterConstraintData(
    ImmutableArray<MustImplementConstraint> mustImplementConstraints,
    ImmutableArray<INamedTypeSymbol> mustNotImplementConstraints,
    ImmutableArray<INamedTypeSymbol> mustHaveAttributeConstraints,
    ImmutableArray<AssemblyNameConstraint> assemblyNameConstraints,
    ImmutableArray<TypeNameConstraint> typeNameConstraints)
{
    public ImmutableArray<MustImplementConstraint> MustImplementConstraints { get; } = mustImplementConstraints;

    public ImmutableArray<INamedTypeSymbol> MustNotImplementConstraints { get; } = mustNotImplementConstraints;

    public ImmutableArray<INamedTypeSymbol> MustHaveAttributeConstraints { get; } = mustHaveAttributeConstraints;

    public ImmutableArray<AssemblyNameConstraint> AssemblyNameConstraints { get; } = assemblyNameConstraints;

    public ImmutableArray<TypeNameConstraint> TypeNameConstraints { get; } = typeNameConstraints;
}

internal readonly struct ParameterConstraintData(
    ImmutableArray<AssemblyNameConstraint> assemblyNameConstraints,
    ImmutableArray<AssignableToConstraint> assignableToConstraints,
    bool requiresOpenGenericType,
    bool requiresReferenceType)
{
    public ImmutableArray<AssemblyNameConstraint> AssemblyNameConstraints { get; } = assemblyNameConstraints;

    public ImmutableArray<AssignableToConstraint> AssignableToConstraints { get; } = assignableToConstraints;

    public bool RequiresOpenGenericType { get; } = requiresOpenGenericType;

    public bool RequiresReferenceType { get; } = requiresReferenceType;
}