using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal static class TypeArgumentConstraintValidator
{
    public static void Validate(
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        ImmutableArray<ITypeSymbol> typeArguments,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintCache cache)
    {
        var typeParameterIndices = BuildTypeParameterIndexMap(typeParameters);

        for (var index = 0; index < typeParameters.Length && index < typeArguments.Length; index++)
        {
            var typeParameter = typeParameters[index];
            var typeArgument = typeArguments[index];
            var constraints = cache.GetTypeParameterConstraints(typeParameter);
            var matches = SymbolMatchHelpers.GetAllMatchingOpenGenerics(typeArgument);

            ValidateMustImplement(constraints, typeArgument, matches, location, reportDiagnostic, cache);
            ValidateMustNotImplement(constraints, typeArgument, matches, location, reportDiagnostic, cache);
            ValidateMustHaveAttribute(constraints, typeArgument, location, reportDiagnostic, cache);
            ValidateAssemblyName(typeParameter, constraints, typeArgument, typeArguments, typeParameterIndices, location,
                reportDiagnostic, cache);
            ValidateTypeName(constraints, typeArgument, location, reportDiagnostic, cache);
        }
    }

    private static void ValidateMustImplement(
        TypeParameterConstraintData constraints,
        ITypeSymbol typeArgument,
        ImmutableArray<INamedTypeSymbol> matches,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintCache cache)
    {
        foreach (var constraint in constraints.MustImplementConstraints)
        {
            var matchCount = SymbolMatchHelpers.CountMatches(matches, constraint.OpenGenericType);
            if (constraint.ExactlyOne)
            {
                if (matchCount is 1 ||
                    HasEquivalentOrStrongerMustImplementConstraint(typeArgument, constraint, cache))
                    continue;

                reportDiagnostic(Diagnostic.Create(
                    ConstraintDiagnostics.MustImplementExactlyOneRule,
                    location,
                    SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                    SymbolMatchHelpers.ToOpenGenericDisplayString(constraint.OpenGenericType)));

                continue;
            }

            if (matchCount > 0 || HasEquivalentOrStrongerMustImplementConstraint(typeArgument, constraint, cache))
                continue;

            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.MustImplementRule,
                location,
                SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                SymbolMatchHelpers.ToOpenGenericDisplayString(constraint.OpenGenericType)));
        }
    }

    private static void ValidateMustNotImplement(
        TypeParameterConstraintData constraints,
        ITypeSymbol typeArgument,
        ImmutableArray<INamedTypeSymbol> matches,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintCache cache)
    {
        foreach (var forbiddenOpenGeneric in constraints.MustNotImplementConstraints)
        {
            if (SymbolMatchHelpers.CountMatches(matches, forbiddenOpenGeneric) is 0 ||
                HasEquivalentMustNotImplementConstraint(typeArgument, forbiddenOpenGeneric, cache))
                continue;

            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.MustNotImplementRule,
                location,
                SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                SymbolMatchHelpers.ToOpenGenericDisplayString(forbiddenOpenGeneric)));
        }
    }

    private static void ValidateMustHaveAttribute(
        TypeParameterConstraintData constraints,
        ITypeSymbol typeArgument,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintCache cache)
    {
        foreach (var requiredAttribute in constraints.MustHaveAttributeConstraints)
        {
            if (SymbolMatchHelpers.TypeHasRequiredAttribute(typeArgument, requiredAttribute) ||
                HasEquivalentMustHaveAttributeConstraint(typeArgument, requiredAttribute, cache))
                continue;

            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.MustHaveAttributeRule,
                location,
                SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                SymbolMatchHelpers.ToMinimalDisplayString(requiredAttribute)));
        }
    }

    private static void ValidateAssemblyName(
        ITypeParameterSymbol typeParameter,
        TypeParameterConstraintData constraints,
        ITypeSymbol typeArgument,
        ImmutableArray<ITypeSymbol> typeArguments,
        Dictionary<string, int> typeParameterIndices,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintCache cache)
    {
        foreach (var assemblyConstraint in constraints.AssemblyNameConstraints)
        {
            if (!typeParameterIndices.TryGetValue(assemblyConstraint.OtherTypeParameterName, out var otherIndex) ||
                otherIndex >= typeArguments.Length ||
                SymbolMatchHelpers.IsWhitelistedType(typeArgument, assemblyConstraint.AllowedTypes))
                continue;

            var otherTypeArgument = typeArguments[otherIndex];
            var expectedAssemblyName = assemblyConstraint.Prefix +
                                       SymbolMatchHelpers.GetAssemblySimpleName(otherTypeArgument.ContainingAssembly) +
                                       assemblyConstraint.Suffix;
            var actualAssemblyName = SymbolMatchHelpers.GetAssemblySimpleName(typeArgument.ContainingAssembly);

            if (string.Equals(expectedAssemblyName, actualAssemblyName, StringComparison.Ordinal) ||
                HasEquivalentOrStrongerAssemblyConstraint(typeArgument, otherTypeArgument, assemblyConstraint, cache))
                continue;

            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.MustMatchAssemblyNameRule,
                location,
                SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                expectedAssemblyName,
                SymbolMatchHelpers.ToMinimalDisplayString(otherTypeArgument)));
        }
    }

    private static void ValidateTypeName(
        TypeParameterConstraintData constraints,
        ITypeSymbol typeArgument,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintCache cache)
    {
        foreach (var typeNameConstraint in constraints.TypeNameConstraints)
        {
            if (TypeNameMatchesConstraint(typeArgument.Name, typeNameConstraint) ||
                HasEquivalentOrStrongerTypeNameConstraint(typeArgument, typeNameConstraint, cache))
                continue;

            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.MustMatchTypeNameRule,
                location,
                SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                typeNameConstraint.Prefix,
                typeNameConstraint.Suffix));
        }
    }

    private static Dictionary<string, int> BuildTypeParameterIndexMap(
        ImmutableArray<ITypeParameterSymbol> typeParameters)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < typeParameters.Length; index++)
            result[typeParameters[index].Name] = index;

        return result;
    }

    private static bool HasEquivalentOrStrongerMustImplementConstraint(
        ITypeSymbol typeArgument,
        MustImplementConstraint requiredConstraint,
        ConstraintCache cache)
    {
        if (typeArgument is not ITypeParameterSymbol typeParameter)
            return false;

        foreach (var candidate in cache.GetTypeParameterConstraints(typeParameter).MustImplementConstraints)
        {
            if (!SymbolEqualityComparer.Default.Equals(candidate.OpenGenericType, requiredConstraint.OpenGenericType))
                continue;

            if (!requiredConstraint.ExactlyOne || candidate.ExactlyOne)
                return true;
        }

        return false;
    }

    private static bool HasEquivalentMustNotImplementConstraint(
        ITypeSymbol typeArgument,
        INamedTypeSymbol forbiddenOpenGeneric,
        ConstraintCache cache)
    {
        if (typeArgument is not ITypeParameterSymbol typeParameter)
            return false;

        foreach (var candidate in cache.GetTypeParameterConstraints(typeParameter).MustNotImplementConstraints)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, forbiddenOpenGeneric))
                return true;
        }

        return false;
    }

    private static bool HasEquivalentMustHaveAttributeConstraint(
        ITypeSymbol typeArgument,
        INamedTypeSymbol requiredAttribute,
        ConstraintCache cache)
    {
        if (typeArgument is not ITypeParameterSymbol typeParameter)
            return false;

        foreach (var candidate in cache.GetTypeParameterConstraints(typeParameter).MustHaveAttributeConstraints)
        {
            if (SymbolEqualityComparer.Default.Equals(candidate, requiredAttribute))
                return true;
        }

        return false;
    }

    private static bool HasEquivalentOrStrongerTypeNameConstraint(
        ITypeSymbol typeArgument,
        TypeNameConstraint requiredConstraint,
        ConstraintCache cache)
    {
        if (typeArgument is not ITypeParameterSymbol typeParameter)
            return false;

        foreach (var candidate in cache.GetTypeParameterConstraints(typeParameter).TypeNameConstraints)
            if (IsEquivalentOrStrongerTypeNameConstraint(candidate, requiredConstraint))
                return true;

        return false;
    }

    internal static bool HasEquivalentOrStrongerAssemblyConstraint(
        ITypeSymbol typeArgument,
        ITypeSymbol otherTypeArgument,
        AssemblyNameConstraint requiredConstraint,
        ConstraintCache cache)
    {
        if (typeArgument is not ITypeParameterSymbol typeParameter)
            return false;

        foreach (var candidate in cache.GetTypeParameterConstraints(typeParameter).AssemblyNameConstraints)
        {
            if (!string.Equals(candidate.Prefix, requiredConstraint.Prefix, StringComparison.Ordinal) ||
                !string.Equals(candidate.Suffix, requiredConstraint.Suffix, StringComparison.Ordinal) ||
                !IsAllowedTypeSubset(candidate.AllowedTypes, requiredConstraint.AllowedTypes))
                continue;

            var candidateOtherTypeParameter = ResolveTypeParameter(typeParameter, candidate.OtherTypeParameterName);
            if (candidateOtherTypeParameter is not null &&
                SymbolEqualityComparer.Default.Equals(candidateOtherTypeParameter, otherTypeArgument))
                return true;
        }

        return false;
    }

    private static bool IsAllowedTypeSubset(
        ImmutableArray<INamedTypeSymbol> candidateAllowedTypes,
        ImmutableArray<INamedTypeSymbol> requiredAllowedTypes) =>
        Enumerable.All(candidateAllowedTypes,
            candidateAllowedType => requiredAllowedTypes.Any(requiredAllowedType =>
                SymbolEqualityComparer.Default.Equals(candidateAllowedType, requiredAllowedType)));

    private static ITypeParameterSymbol? ResolveTypeParameter(ITypeParameterSymbol typeParameter,
        string typeParameterName)
    {
        var declaringSymbol = typeParameter.ContainingSymbol;
        var typeParameters = declaringSymbol switch
        {
            IMethodSymbol method => method.TypeParameters,
            INamedTypeSymbol namedType => namedType.TypeParameters,
            _ => []
        };

        return typeParameters.FirstOrDefault(candidate =>
            string.Equals(candidate.Name, typeParameterName, StringComparison.Ordinal));
    }

    private static bool TypeNameMatchesConstraint(string typeName, TypeNameConstraint constraint) =>
        typeName.StartsWith(constraint.Prefix, StringComparison.Ordinal) &&
        typeName.EndsWith(constraint.Suffix, StringComparison.Ordinal);

    private static bool IsEquivalentOrStrongerTypeNameConstraint(
        TypeNameConstraint candidate,
        TypeNameConstraint requiredConstraint) =>
        candidate.Prefix.StartsWith(requiredConstraint.Prefix, StringComparison.Ordinal) &&
        candidate.Suffix.EndsWith(requiredConstraint.Suffix, StringComparison.Ordinal);
}
