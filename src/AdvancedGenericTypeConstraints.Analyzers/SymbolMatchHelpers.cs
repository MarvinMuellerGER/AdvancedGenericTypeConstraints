using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal static class SymbolMatchHelpers
{
    public static ImmutableArray<INamedTypeSymbol> GetAllMatchingOpenGenerics(ITypeSymbol typeArgument)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        if (typeArgument is INamedTypeSymbol namedType)
        {
            AddIfGeneric(builder, namedType);

            for (var baseType = namedType.BaseType; baseType is not null; baseType = baseType.BaseType)
                AddIfGeneric(builder, baseType);
        }

        foreach (var implementedInterface in typeArgument.AllInterfaces)
            AddIfGeneric(builder, implementedInterface);

        return builder.ToImmutable();
    }

    public static int CountMatches(ImmutableArray<INamedTypeSymbol> matches, INamedTypeSymbol openGenericType) =>
        matches.Count(match => SymbolEqualityComparer.Default.Equals(match.OriginalDefinition, openGenericType));

    public static bool TypeHasRequiredAttribute(ITypeSymbol typeArgument, INamedTypeSymbol requiredAttribute) =>
        typeArgument.GetAttributes().Any(attribute =>
            attribute.AttributeClass is not null && IsSameOrDerivedAttribute(attribute.AttributeClass, requiredAttribute));

    public static bool IsWhitelistedType(ITypeSymbol typeArgument, ImmutableArray<INamedTypeSymbol> allowedTypes) =>
        allowedTypes.Any(allowedType => SymbolEqualityComparer.Default.Equals(typeArgument, allowedType));

    public static bool IsOpenGenericTypeDefinition(ITypeSymbol typeSymbol) =>
        typeSymbol is INamedTypeSymbol { IsGenericType: true } namedType &&
        (namedType.IsUnboundGenericType ||
         SymbolEqualityComparer.Default.Equals(namedType, namedType.OriginalDefinition));

    public static string ToOpenGenericDisplayString(INamedTypeSymbol openGenericType) => openGenericType
        .ConstructUnboundGenericType().ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    public static string ToMinimalDisplayString(ITypeSymbol typeSymbol) =>
        typeSymbol.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    public static string GetAssemblySimpleName(IAssemblySymbol? assembly) => assembly?.Name ?? string.Empty;

    private static bool IsSameOrDerivedAttribute(INamedTypeSymbol attributeClass, INamedTypeSymbol requiredAttribute)
    {
        for (var current = attributeClass; current is not null; current = current.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(current, requiredAttribute))
                return true;
        }

        return false;
    }

    private static void AddIfGeneric(ImmutableArray<INamedTypeSymbol>.Builder builder, INamedTypeSymbol namedType)
    {
        if (namedType.IsGenericType)
            builder.Add(namedType.OriginalDefinition);
    }
}
