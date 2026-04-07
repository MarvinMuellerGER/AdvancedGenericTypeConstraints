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

    public static bool IsReferenceType(ITypeSymbol typeSymbol) => !typeSymbol.IsValueType;

    public static bool IsAssignableTo(ITypeSymbol sourceType, ITypeSymbol targetType)
    {
        if (SymbolEqualityComparer.Default.Equals(sourceType, targetType))
            return true;

        if (sourceType is not INamedTypeSymbol sourceNamedType || targetType is not INamedTypeSymbol targetNamedType)
            return false;

        if (targetNamedType.IsGenericType)
        {
            var targetOpenGeneric = targetNamedType.OriginalDefinition;
            if (CountMatches(GetAllMatchingOpenGenerics(sourceType), targetOpenGeneric) > 0)
                return true;

            if (sourceNamedType.IsUnboundGenericType &&
                CountMatches(GetAllMatchingOpenGenerics(sourceNamedType.OriginalDefinition), targetOpenGeneric) > 0)
                return true;
        }

        var normalizedTarget = NormalizeForAssignability(targetNamedType);
        if (SymbolEqualityComparer.Default.Equals(NormalizeForAssignability(sourceNamedType), normalizedTarget))
            return true;

        for (var baseType = sourceNamedType.BaseType; baseType is not null; baseType = baseType.BaseType)
            if (SymbolEqualityComparer.Default.Equals(NormalizeForAssignability(baseType), normalizedTarget))
                return true;

        return sourceType.AllInterfaces.Any(implementedInterface =>
            SymbolEqualityComparer.Default.Equals(NormalizeForAssignability(implementedInterface), normalizedTarget));
    }

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

    private static INamedTypeSymbol NormalizeForAssignability(INamedTypeSymbol namedType) =>
        namedType.IsGenericType ? namedType.OriginalDefinition : namedType;
}
