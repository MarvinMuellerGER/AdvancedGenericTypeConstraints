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
            ValidateAssemblyName(typeParameter, typeArgument, typeArguments, typeParameterIndices, location, reportDiagnostic, symbols);
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
        foreach (var constraint in ConstraintReaders.GetMustImplementConstraints(typeParameter, symbols.MustImplementAttribute))
        {
            var matchCount = SymbolMatchHelpers.CountMatches(matches, constraint.OpenGenericType);
            if (constraint.ExactlyOne)
            {
                if (matchCount is 1)
                    continue;

                reportDiagnostic(Diagnostic.Create(
                    ConstraintDiagnostics.MustImplementExactlyOneRule,
                    location,
                    SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                    SymbolMatchHelpers.ToOpenGenericDisplayString(constraint.OpenGenericType)));

                continue;
            }

            if (matchCount > 0)
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
        foreach (var forbiddenOpenGeneric in ConstraintReaders.GetMustNotImplementConstraints(typeParameter, symbols.MustNotImplementAttribute)
                     .Where(forbiddenOpenGeneric => SymbolMatchHelpers.CountMatches(matches, forbiddenOpenGeneric) is not 0))
        {
            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.MustNotImplementRule,
                location,
                SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                SymbolMatchHelpers.ToOpenGenericDisplayString(forbiddenOpenGeneric)));
        }
    }

    private static void ValidateMustHaveAttribute(
        ITypeParameterSymbol typeParameter,
        ITypeSymbol typeArgument,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        ConstraintAttributeSymbols symbols)
    {
        foreach (var requiredAttribute in ConstraintReaders.GetMustHaveAttributeConstraints(typeParameter, symbols.MustHaveAttribute))
        {
            if (SymbolMatchHelpers.TypeHasRequiredAttribute(typeArgument, requiredAttribute))
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

            if (string.Equals(expectedAssemblyName, actualAssemblyName, StringComparison.Ordinal))
                continue;

            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.MustMatchAssemblyNameRule,
                location,
                SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                expectedAssemblyName,
                SymbolMatchHelpers.ToMinimalDisplayString(otherTypeArgument)));
        }
    }

    private static Dictionary<string, int> BuildTypeParameterIndexMap(ImmutableArray<ITypeParameterSymbol> typeParameters)
    {
        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var index = 0; index < typeParameters.Length; index++)
            result[typeParameters[index].Name] = index;

        return result;
    }
}
