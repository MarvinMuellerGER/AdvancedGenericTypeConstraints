using System.Collections.Concurrent;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal sealed class ConstraintCache(Compilation compilation, ConstraintAttributeSymbols symbols)
{
    private readonly ConcurrentDictionary<IMethodSymbol, bool> _methodHasInvocationConstraints =
        new(SymbolEqualityComparer.Default);

    private readonly ConcurrentDictionary<IMethodSymbol, Dictionary<string, int>> _parameterIndexMaps =
        new(SymbolEqualityComparer.Default);

    private readonly ConcurrentDictionary<IParameterSymbol, ParameterConstraintData> _parameterConstraints =
        new(SymbolEqualityComparer.Default);

    private readonly INamedTypeSymbol? _systemTypeSymbol = compilation.GetTypeByMetadataName("System.Type");

    private readonly ConcurrentDictionary<ITypeSymbol, OpenGenericMatchData> _typeMatches =
        new(SymbolEqualityComparer.Default);

    private readonly ConcurrentDictionary<ITypeSymbol, ImmutableArray<INamedTypeSymbol>> _typeAttributeClasses =
        new(SymbolEqualityComparer.Default);

    private readonly ConcurrentDictionary<INamedTypeSymbol, bool> _typeHasTypeArgumentConstraints =
        new(SymbolEqualityComparer.Default);

    private readonly ConcurrentDictionary<ISymbol, Dictionary<string, ITypeParameterSymbol>> _typeParameterMaps =
        new(SymbolEqualityComparer.Default);

    private readonly ConcurrentDictionary<ITypeParameterSymbol, TypeParameterConstraintData> _typeParameterConstraints =
        new(SymbolEqualityComparer.Default);

    public TypeParameterConstraintData GetTypeParameterConstraints(ITypeParameterSymbol typeParameter) =>
        _typeParameterConstraints.GetOrAdd(typeParameter, CreateTypeParameterConstraintData);

    public ParameterConstraintData GetParameterConstraints(IParameterSymbol parameter) =>
        _parameterConstraints.GetOrAdd(parameter, CreateParameterConstraintData);

    public OpenGenericMatchData GetOpenGenericMatchData(ITypeSymbol typeSymbol) =>
        _typeMatches.GetOrAdd(typeSymbol, CreateOpenGenericMatchData);

    public IReadOnlyDictionary<string, int> GetParameterIndexMap(IMethodSymbol method) =>
        _parameterIndexMaps.GetOrAdd(method, CreateParameterIndexMap);

    public bool HasRelevantInvocationConstraints(IMethodSymbol method) =>
        _methodHasInvocationConstraints.GetOrAdd(method, HasRelevantInvocationConstraintsCore);

    public bool HasRelevantTypeArgumentConstraints(INamedTypeSymbol namedType) =>
        _typeHasTypeArgumentConstraints.GetOrAdd(namedType.OriginalDefinition, HasRelevantTypeArgumentConstraintsCore);

    public bool TypeHasRequiredAttribute(ITypeSymbol typeSymbol, INamedTypeSymbol requiredAttribute)
    {
        foreach (var attributeClass in _typeAttributeClasses.GetOrAdd(typeSymbol, CreateTypeAttributeClasses))
        {
            if (SymbolEqualityComparer.Default.Equals(attributeClass, requiredAttribute))
                return true;
        }

        return false;
    }

    public IReadOnlyDictionary<string, ITypeParameterSymbol> GetTypeParameterMap(ISymbol owner) =>
        _typeParameterMaps.GetOrAdd(owner, CreateTypeParameterMap);

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

    private bool HasRelevantInvocationConstraintsCore(IMethodSymbol method)
    {
        foreach (var typeParameter in method.TypeParameters)
        {
            if (GetTypeParameterConstraints(typeParameter).HasAny)
                return true;
        }

        foreach (var parameter in method.Parameters)
        {
            if (GetParameterConstraints(parameter).HasAny)
                return true;
        }

        return false;
    }

    private bool HasRelevantTypeArgumentConstraintsCore(INamedTypeSymbol namedType)
    {
        foreach (var typeParameter in namedType.TypeParameters)
        {
            if (GetTypeParameterConstraints(typeParameter).HasAny)
                return true;
        }

        return false;
    }

    private static OpenGenericMatchData CreateOpenGenericMatchData(ITypeSymbol typeSymbol)
    {
        var matches = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var counts = new Dictionary<INamedTypeSymbol, int>(SymbolEqualityComparer.Default);

        if (typeSymbol is INamedTypeSymbol namedType)
        {
            AddIfGeneric(namedType);

            for (var baseType = namedType.BaseType; baseType is not null; baseType = baseType.BaseType)
                AddIfGeneric(baseType);
        }

        foreach (var implementedInterface in typeSymbol.AllInterfaces)
            AddIfGeneric(implementedInterface);

        return new OpenGenericMatchData(matches.ToImmutable(), counts);

        void AddIfGeneric(INamedTypeSymbol namedTypeSymbol)
        {
            if (!namedTypeSymbol.IsGenericType)
                return;

            var definition = namedTypeSymbol.OriginalDefinition;
            matches.Add(definition);
            counts.TryGetValue(definition, out var count);
            counts[definition] = count + 1;
        }
    }

    private static ImmutableArray<INamedTypeSymbol> CreateTypeAttributeClasses(ITypeSymbol typeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        foreach (var attribute in typeSymbol.GetAttributes())
        {
            for (var current = attribute.AttributeClass; current is not null; current = current.BaseType)
                builder.Add(current);
        }

        return builder.ToImmutable();
    }

    private static Dictionary<string, int> CreateParameterIndexMap(IMethodSymbol method)
    {
        var result = new Dictionary<string, int>(method.Parameters.Length, StringComparer.Ordinal);
        for (var index = 0; index < method.Parameters.Length; index++)
            result[method.Parameters[index].Name] = index;

        return result;
    }

    private static Dictionary<string, ITypeParameterSymbol> CreateTypeParameterMap(ISymbol owner)
    {
        var typeParameters = owner switch
        {
            IMethodSymbol method => method.TypeParameters,
            INamedTypeSymbol namedType => namedType.TypeParameters,
            _ => []
        };

        var result = new Dictionary<string, ITypeParameterSymbol>(typeParameters.Length, StringComparer.Ordinal);
        foreach (var typeParameter in typeParameters)
            result[typeParameter.Name] = typeParameter;

        return result;
    }
}

internal readonly struct TypeParameterConstraintData(
    ImmutableArray<MustImplementConstraint> mustImplementConstraints,
    ImmutableArray<INamedTypeSymbol> mustNotImplementConstraints,
    ImmutableArray<INamedTypeSymbol> mustHaveAttributeConstraints,
    ImmutableArray<AssemblyNameConstraint> assemblyNameConstraints,
    ImmutableArray<TypeNameConstraint> typeNameConstraints)
{
    public bool HasAny =>
        !MustImplementConstraints.IsDefaultOrEmpty ||
        !MustNotImplementConstraints.IsDefaultOrEmpty ||
        !MustHaveAttributeConstraints.IsDefaultOrEmpty ||
        !AssemblyNameConstraints.IsDefaultOrEmpty ||
        !TypeNameConstraints.IsDefaultOrEmpty;

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
    public bool HasAny =>
        !AssemblyNameConstraints.IsDefaultOrEmpty ||
        !AssignableToConstraints.IsDefaultOrEmpty ||
        RequiresOpenGenericType ||
        RequiresReferenceType;

    public ImmutableArray<AssemblyNameConstraint> AssemblyNameConstraints { get; } = assemblyNameConstraints;

    public ImmutableArray<AssignableToConstraint> AssignableToConstraints { get; } = assignableToConstraints;

    public bool RequiresOpenGenericType { get; } = requiresOpenGenericType;

    public bool RequiresReferenceType { get; } = requiresReferenceType;
}

internal readonly struct OpenGenericMatchData(
    ImmutableArray<INamedTypeSymbol> matches,
    Dictionary<INamedTypeSymbol, int> counts)
{
    public ImmutableArray<INamedTypeSymbol> Matches { get; } = matches;

    public int CountMatches(INamedTypeSymbol openGenericType) =>
        counts.TryGetValue(openGenericType, out var count)
            ? count
            : 0;
}
