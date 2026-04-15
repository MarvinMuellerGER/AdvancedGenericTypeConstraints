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
        var owner = typeParameters.Length > 0 ? typeParameters[0].ContainingSymbol : null;
        var typeParameterIndices = owner is not null
            ? cache.GetTypeParameterMap(owner)
            : EmptyTypeParameterMap;

        for (var index = 0; index < typeParameters.Length && index < typeArguments.Length; index++)
        {
            var typeParameter = typeParameters[index];
            var typeArgument = typeArguments[index];
            var constraints = cache.GetTypeParameterConstraints(typeParameter);
            if (!constraints.HasAny)
                continue;

            var matchData = cache.GetOpenGenericMatchData(typeArgument);

            ValidateMustImplement(constraints, typeArgument, matchData, location, reportDiagnostic, cache);
            ValidateMustNotImplement(constraints, typeArgument, matchData, location, reportDiagnostic, cache);
            ValidateMustHaveAttribute(constraints, typeArgument, location, reportDiagnostic, cache);
            ValidateAssemblyName(typeParameter, constraints, typeArgument, typeArguments, typeParameterIndices, location,
                reportDiagnostic, cache);
            ValidateTypeName(constraints, typeArgument, location, reportDiagnostic, cache);
        }
    }

    private static void ValidateMustImplement(
        TypeParameterConstraintData constraints,
        ITypeSymbol typeArgument,
        OpenGenericMatchData matchData,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintCache cache)
    {
        foreach (var constraint in constraints.MustImplementConstraints)
        {
            var matchCount = matchData.CountMatches(constraint.OpenGenericType);
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
        OpenGenericMatchData matchData,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintCache cache)
    {
        foreach (var forbiddenOpenGeneric in constraints.MustNotImplementConstraints)
        {
            if (matchData.CountMatches(forbiddenOpenGeneric) is 0 ||
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
            if (cache.TypeHasRequiredAttribute(typeArgument, requiredAttribute) ||
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
        IReadOnlyDictionary<string, ITypeParameterSymbol> typeParameterIndices,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintCache cache)
    {
        foreach (var assemblyConstraint in constraints.AssemblyNameConstraints)
        {
            if (!typeParameterIndices.TryGetValue(assemblyConstraint.OtherTypeParameterName, out var otherTypeParameter) ||
                SymbolMatchHelpers.IsWhitelistedType(typeArgument, assemblyConstraint.AllowedTypes))
                continue;

            var otherIndex = otherTypeParameter.Ordinal;
            if (otherIndex >= typeArguments.Length)
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

            if (cache.GetTypeParameterMap(typeParameter.ContainingSymbol)
                    .TryGetValue(candidate.OtherTypeParameterName, out var candidateOtherTypeParameter) &&
                SymbolEqualityComparer.Default.Equals(candidateOtherTypeParameter, otherTypeArgument))
                return true;
        }

        return false;
    }

    private static bool IsAllowedTypeSubset(
        ImmutableArray<INamedTypeSymbol> candidateAllowedTypes,
        ImmutableArray<INamedTypeSymbol> requiredAllowedTypes)
    {
        foreach (var candidateAllowedType in candidateAllowedTypes)
        {
            var found = false;
            foreach (var requiredAllowedType in requiredAllowedTypes)
            {
                if (!SymbolEqualityComparer.Default.Equals(candidateAllowedType, requiredAllowedType))
                    continue;

                found = true;
                break;
            }

            if (!found)
                return false;
        }

        return true;
    }

    private static bool TypeNameMatchesConstraint(string typeName, TypeNameConstraint constraint) =>
        typeName.StartsWith(constraint.Prefix, StringComparison.Ordinal) &&
        typeName.EndsWith(constraint.Suffix, StringComparison.Ordinal);

    private static bool IsEquivalentOrStrongerTypeNameConstraint(
        TypeNameConstraint candidate,
        TypeNameConstraint requiredConstraint) =>
        candidate.Prefix.StartsWith(requiredConstraint.Prefix, StringComparison.Ordinal) &&
        candidate.Suffix.EndsWith(requiredConstraint.Suffix, StringComparison.Ordinal);

    private static readonly IReadOnlyDictionary<string, ITypeParameterSymbol> EmptyTypeParameterMap =
        new Dictionary<string, ITypeParameterSymbol>(0, StringComparer.Ordinal);
}
