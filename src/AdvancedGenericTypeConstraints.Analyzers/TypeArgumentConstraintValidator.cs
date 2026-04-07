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
        ConstraintAttributeSymbols symbols)
    {
        var typeParameterIndices = BuildTypeParameterIndexMap(typeParameters);

        for (var index = 0; index < typeParameters.Length && index < typeArguments.Length; index++)
        {
            var typeParameter = typeParameters[index];
            var typeArgument = typeArguments[index];
            var matches = SymbolMatchHelpers.GetAllMatchingOpenGenerics(typeArgument);

            ValidateMustImplement(typeParameter, typeArgument, matches, location, reportDiagnostic, symbols);
            ValidateMustNotImplement(typeParameter, typeArgument, matches, location, reportDiagnostic, symbols);
            ValidateMustHaveAttribute(typeParameter, typeArgument, location, reportDiagnostic, symbols);
            ValidateAssemblyName(typeParameter, typeArgument, typeArguments, typeParameterIndices, location,
                reportDiagnostic, symbols);
        }
    }

    private static void ValidateMustImplement(
        ITypeParameterSymbol typeParameter,
        ITypeSymbol typeArgument,
        ImmutableArray<INamedTypeSymbol> matches,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintAttributeSymbols symbols)
    {
        foreach (var constraint in ConstraintReaders.GetMustImplementConstraints(typeParameter,
                     symbols.MustImplementAttribute))
        {
            var matchCount = SymbolMatchHelpers.CountMatches(matches, constraint.OpenGenericType);
            if (constraint.ExactlyOne)
            {
                if (matchCount is 1 ||
                    HasEquivalentOrStrongerMustImplementConstraint(typeArgument, constraint, symbols))
                    continue;

                reportDiagnostic(Diagnostic.Create(
                    ConstraintDiagnostics.MustImplementExactlyOneRule,
                    location,
                    SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                    SymbolMatchHelpers.ToOpenGenericDisplayString(constraint.OpenGenericType)));

                continue;
            }

            if (matchCount > 0 || HasEquivalentOrStrongerMustImplementConstraint(typeArgument, constraint, symbols))
                continue;

            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.MustImplementRule,
                location,
                SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                SymbolMatchHelpers.ToOpenGenericDisplayString(constraint.OpenGenericType)));
        }
    }

    private static void ValidateMustNotImplement(
        ITypeParameterSymbol typeParameter,
        ITypeSymbol typeArgument,
        ImmutableArray<INamedTypeSymbol> matches,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintAttributeSymbols symbols)
    {
        foreach (var forbiddenOpenGeneric in ConstraintReaders
                     .GetMustNotImplementConstraints(typeParameter, symbols.MustNotImplementAttribute)
                     .Where(forbiddenOpenGeneric =>
                         SymbolMatchHelpers.CountMatches(matches, forbiddenOpenGeneric) is not 0 &&
                         !HasEquivalentMustNotImplementConstraint(typeArgument, forbiddenOpenGeneric, symbols)))
            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.MustNotImplementRule,
                location,
                SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                SymbolMatchHelpers.ToOpenGenericDisplayString(forbiddenOpenGeneric)));
    }

    private static void ValidateMustHaveAttribute(
        ITypeParameterSymbol typeParameter,
        ITypeSymbol typeArgument,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintAttributeSymbols symbols)
    {
        foreach (var requiredAttribute in ConstraintReaders.GetMustHaveAttributeConstraints(typeParameter,
                     symbols.MustHaveAttribute))
        {
            if (SymbolMatchHelpers.TypeHasRequiredAttribute(typeArgument, requiredAttribute) ||
                HasEquivalentMustHaveAttributeConstraint(typeArgument, requiredAttribute, symbols))
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
        ITypeSymbol typeArgument,
        ImmutableArray<ITypeSymbol> typeArguments,
        Dictionary<string, int> typeParameterIndices,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintAttributeSymbols symbols)
    {
        foreach (var assemblyConstraint in ConstraintReaders.GetAssemblyNameConstraints(
                     typeParameter,
                     symbols.MustMatchAssemblyNameOfAttribute))
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
                HasEquivalentOrStrongerAssemblyConstraint(typeArgument, otherTypeArgument, assemblyConstraint, symbols))
                continue;

            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.MustMatchAssemblyNameRule,
                location,
                SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                expectedAssemblyName,
                SymbolMatchHelpers.ToMinimalDisplayString(otherTypeArgument)));
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
        ConstraintAttributeSymbols symbols)
    {
        if (typeArgument is not ITypeParameterSymbol typeParameter)
            return false;

        foreach (var candidate in ConstraintReaders.GetMustImplementConstraints(typeParameter,
                     symbols.MustImplementAttribute))
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
        ConstraintAttributeSymbols symbols)
    {
        if (typeArgument is not ITypeParameterSymbol typeParameter)
            return false;

        return ConstraintReaders.GetMustNotImplementConstraints(typeParameter, symbols.MustNotImplementAttribute)
            .Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, forbiddenOpenGeneric));
    }

    private static bool HasEquivalentMustHaveAttributeConstraint(
        ITypeSymbol typeArgument,
        INamedTypeSymbol requiredAttribute,
        ConstraintAttributeSymbols symbols)
    {
        if (typeArgument is not ITypeParameterSymbol typeParameter)
            return false;

        return ConstraintReaders.GetMustHaveAttributeConstraints(typeParameter, symbols.MustHaveAttribute)
            .Any(candidate => SymbolEqualityComparer.Default.Equals(candidate, requiredAttribute));
    }

    internal static bool HasEquivalentOrStrongerAssemblyConstraint(
        ITypeSymbol typeArgument,
        ITypeSymbol otherTypeArgument,
        AssemblyNameConstraint requiredConstraint,
        ConstraintAttributeSymbols symbols)
    {
        if (typeArgument is not ITypeParameterSymbol typeParameter)
            return false;

        foreach (var candidate in ConstraintReaders.GetAssemblyNameConstraints(
                     typeParameter,
                     symbols.MustMatchAssemblyNameOfAttribute))
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
}
