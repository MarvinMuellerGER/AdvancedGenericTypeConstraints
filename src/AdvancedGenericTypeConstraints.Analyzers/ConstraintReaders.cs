using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal static class ConstraintReaders
{
    public static ImmutableArray<MustImplementConstraint> GetMustImplementConstraints(
        ITypeParameterSymbol typeParameter,
        INamedTypeSymbol? attributeSymbol)
    {
        if (attributeSymbol is null)
            return [];

        var builder = ImmutableArray.CreateBuilder<MustImplementConstraint>();
        var relevantAttributes = typeParameter.GetAttributes().Where(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol) &&
            attribute.ConstructorArguments.Length is 1 or 2);

        foreach (var attribute in relevantAttributes)
        {
            if (attribute.ConstructorArguments[0].Value is not INamedTypeSymbol openGenericType)
                continue;

            var exactlyOne = attribute.ConstructorArguments.Length is 2 &&
                             attribute.ConstructorArguments[1].Value is true;

            builder.Add(new MustImplementConstraint(openGenericType.OriginalDefinition, exactlyOne));
        }

        return builder.ToImmutable();
    }

    public static ImmutableArray<INamedTypeSymbol> GetMustNotImplementConstraints(
        ITypeParameterSymbol typeParameter,
        INamedTypeSymbol? attributeSymbol)
    {
        if (attributeSymbol is null)
            return [];

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var relevantAttributes = typeParameter.GetAttributes().Where(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol) &&
            attribute.ConstructorArguments.Length is 1);

        foreach (var attribute in relevantAttributes)
            if (attribute.ConstructorArguments[0].Value is INamedTypeSymbol openGenericType)
                builder.Add(openGenericType.OriginalDefinition);

        return builder.ToImmutable();
    }

    public static ImmutableArray<INamedTypeSymbol> GetMustHaveAttributeConstraints(
        ITypeParameterSymbol typeParameter,
        INamedTypeSymbol? attributeSymbol)
    {
        if (attributeSymbol is null)
            return [];

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();
        var relevantAttributes = typeParameter.GetAttributes().Where(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol) &&
            attribute.ConstructorArguments.Length is 1 &&
            attribute.ConstructorArguments[0].Value is INamedTypeSymbol);

        foreach (var attribute in relevantAttributes)
            builder.Add((INamedTypeSymbol)attribute.ConstructorArguments[0].Value!);

        return builder.ToImmutable();
    }

    public static ImmutableArray<AssemblyNameConstraint> GetAssemblyNameConstraints(
        ITypeParameterSymbol typeParameter,
        INamedTypeSymbol? attributeSymbol)
        => GetAssemblyNameConstraints(typeParameter.GetAttributes(), attributeSymbol);

    public static ImmutableArray<AssemblyNameConstraint> GetAssemblyNameConstraints(
        IParameterSymbol parameter,
        INamedTypeSymbol? attributeSymbol)
        => GetAssemblyNameConstraints(parameter.GetAttributes(), attributeSymbol);

    internal static ImmutableArray<AssemblyNameConstraint> GetAssemblyNameConstraints(
        ImmutableArray<AttributeData> attributes,
        INamedTypeSymbol? attributeSymbol)
    {
        if (attributeSymbol is null)
            return [];

        var builder = ImmutableArray.CreateBuilder<AssemblyNameConstraint>();
        var relevantAttributes = attributes.Where(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol) &&
            attribute.ConstructorArguments.Length is >= 1 and <= 3 &&
            attribute.ConstructorArguments[0].Value is string);

        foreach (var attribute in relevantAttributes)
            builder.Add(new AssemblyNameConstraint(
                (string)attribute.ConstructorArguments[0].Value!,
                attribute.ConstructorArguments.Length >= 2
                    ? attribute.ConstructorArguments[1].Value as string ?? string.Empty
                    : string.Empty,
                attribute.ConstructorArguments.Length >= 3
                    ? attribute.ConstructorArguments[2].Value as string ?? string.Empty
                    : string.Empty,
                GetAllowedTypes(attribute)));

        return builder.ToImmutable();
    }

    public static ImmutableArray<AssignableToConstraint> GetAssignableToConstraints(
        IParameterSymbol parameter,
        INamedTypeSymbol? attributeSymbol)
    {
        if (attributeSymbol is null)
            return [];

        var builder = ImmutableArray.CreateBuilder<AssignableToConstraint>();
        var relevantAttributes = parameter.GetAttributes().Where(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol) &&
            attribute.ConstructorArguments.Length is 1 &&
            attribute.ConstructorArguments[0].Value is string);

        foreach (var attribute in relevantAttributes)
            builder.Add(new AssignableToConstraint((string)attribute.ConstructorArguments[0].Value!));

        return builder.ToImmutable();
    }

    internal static ImmutableArray<AssignableToConstraint> GetAssignableToConstraints(
        ImmutableArray<AttributeData> attributes,
        INamedTypeSymbol? attributeSymbol)
    {
        if (attributeSymbol is null)
            return [];

        var builder = ImmutableArray.CreateBuilder<AssignableToConstraint>();
        var relevantAttributes = attributes.Where(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol) &&
            attribute.ConstructorArguments.Length is 1 &&
            attribute.ConstructorArguments[0].Value is string);

        foreach (var attribute in relevantAttributes)
            builder.Add(new AssignableToConstraint((string)attribute.ConstructorArguments[0].Value!));

        return builder.ToImmutable();
    }

    public static ImmutableArray<TypeNameConstraint> GetTypeNameConstraints(
        ITypeParameterSymbol typeParameter,
        INamedTypeSymbol? attributeSymbol)
    {
        if (attributeSymbol is null)
            return [];

        var builder = ImmutableArray.CreateBuilder<TypeNameConstraint>();
        var relevantAttributes = typeParameter.GetAttributes().Where(attribute =>
            SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol) &&
            attribute.ConstructorArguments.Length is >= 0 and <= 2);

        foreach (var attribute in relevantAttributes)
            builder.Add(new TypeNameConstraint(
                attribute.ConstructorArguments.Length >= 1
                    ? attribute.ConstructorArguments[0].Value as string ?? string.Empty
                    : string.Empty,
                attribute.ConstructorArguments.Length is 2
                    ? attribute.ConstructorArguments[1].Value as string ?? string.Empty
                    : string.Empty));

        return builder.ToImmutable();
    }

    private static ImmutableArray<INamedTypeSymbol> GetAllowedTypes(AttributeData attribute)
    {
        foreach (var namedArgument in attribute.NamedArguments.Where(namedArgument =>
                     string.Equals(namedArgument.Key, "AllowedTypes", StringComparison.Ordinal) &&
                     namedArgument.Value.Kind is TypedConstantKind.Array))
            return
            [
                ..namedArgument.Value.Values
                    .Where(static value => value.Value is INamedTypeSymbol)
                    .Select(static value => (INamedTypeSymbol)value.Value!)
            ];

        return [];
    }
}
