using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal static class ConstraintConfigurationValidator
{
    public static void Validate(
        IMethodSymbol method,
        ConstraintAttributeSymbols symbols,
        Action<Diagnostic> reportDiagnostic)
    {
        Validate(method.TypeParameters, symbols, reportDiagnostic);

        if (symbols.MustMatchAssemblyNameOfAttribute is not null)
            ValidateAssemblyConstraintConfiguration(
                method.Parameters,
                symbols.MustMatchAssemblyNameOfAttribute,
                reportDiagnostic);

        if (symbols.MustBeAssignableToAttribute is not null)
            ValidateAssignableToConstraintConfiguration(
                method.Parameters,
                symbols.MustBeAssignableToAttribute,
                reportDiagnostic);
    }

    public static void Validate(
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        ConstraintAttributeSymbols symbols,
        Action<Diagnostic> reportDiagnostic)
    {
        foreach (var typeParameter in typeParameters)
        {
            if (symbols.MustImplementAttribute is not null)
                ValidateMustImplementConstraintConfiguration(typeParameter, symbols.MustImplementAttribute, reportDiagnostic);

            if (symbols.MustMatchAssemblyNameOfAttribute is not null)
                ValidateAssemblyConstraintConfiguration(
                    typeParameter,
                    typeParameters,
                    symbols.MustMatchAssemblyNameOfAttribute,
                    reportDiagnostic);
        }
    }

    private static void ValidateAssemblyConstraintConfiguration(
        ImmutableArray<IParameterSymbol> parameters,
        INamedTypeSymbol attributeSymbol,
        Action<Diagnostic> reportDiagnostic)
    {
        var availableNames = new HashSet<string>(
            parameters.Select(static parameter => parameter.Name),
            StringComparer.Ordinal);

        foreach (var parameter in parameters)
        foreach (var attribute in parameter.GetAttributes().Where(attribute =>
                     SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol) &&
                     attribute.ConstructorArguments.Length is >= 1 and <= 3 &&
                     attribute.ConstructorArguments[0].Value is string))
        {
            var otherParameterName = (string)attribute.ConstructorArguments[0].Value!;
            var isInvalidReference = string.Equals(otherParameterName, parameter.Name, StringComparison.Ordinal) ||
                                     !availableNames.Contains(otherParameterName);
            var isInvalidTypeUsage = !IsSystemType(parameter.Type) ||
                                     !parameters.Any(candidate =>
                                         string.Equals(candidate.Name, otherParameterName, StringComparison.Ordinal) &&
                                         IsSystemType(candidate.Type));

            if (!isInvalidReference && !isInvalidTypeUsage)
                continue;

            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.InvalidAssemblyConstraintConfigurationRule,
                GetAttributeLocation(attribute, parameter),
                parameter.Name,
                otherParameterName));
        }
    }

    private static void ValidateMustImplementConstraintConfiguration(
        ITypeParameterSymbol typeParameter,
        INamedTypeSymbol attributeSymbol,
        Action<Diagnostic> reportDiagnostic)
    {
        var nonInterfaceAttributes = typeParameter.GetAttributes()
            .Where(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol) &&
                attribute.ConstructorArguments.Length is 1 or 2 &&
                attribute.ConstructorArguments[0].Value is INamedTypeSymbol { TypeKind: not TypeKind.Interface })
            .ToArray();

        if (nonInterfaceAttributes.Length <= 1)
            return;

        foreach (var attribute in nonInterfaceAttributes.Skip(1))
        {
            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.InvalidMustImplementConfigurationRule,
                GetAttributeLocation(attribute, typeParameter),
                typeParameter.Name));
        }
    }

    private static void ValidateAssemblyConstraintConfiguration(
        ITypeParameterSymbol typeParameter,
        ImmutableArray<ITypeParameterSymbol> availableTypeParameters,
        INamedTypeSymbol attributeSymbol,
        Action<Diagnostic> reportDiagnostic)
    {
        var availableNames = new HashSet<string>(
            availableTypeParameters.Select(static parameter => parameter.Name),
            StringComparer.Ordinal);

        foreach (var attribute in typeParameter.GetAttributes().Where(attribute =>
                     SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol) &&
                     attribute.ConstructorArguments.Length is >= 1 and <= 3 &&
                     attribute.ConstructorArguments[0].Value is string))
        {
            var otherTypeParameterName = (string)attribute.ConstructorArguments[0].Value!;
            if (string.Equals(otherTypeParameterName, typeParameter.Name, StringComparison.Ordinal) ||
                !availableNames.Contains(otherTypeParameterName))
            {
                reportDiagnostic(Diagnostic.Create(
                    ConstraintDiagnostics.InvalidAssemblyConstraintConfigurationRule,
                    GetAttributeLocation(attribute, typeParameter),
                    typeParameter.Name,
                otherTypeParameterName));
            }
        }
    }

    private static void ValidateAssignableToConstraintConfiguration(
        ImmutableArray<IParameterSymbol> parameters,
        INamedTypeSymbol attributeSymbol,
        Action<Diagnostic> reportDiagnostic)
    {
        var availableNames = new HashSet<string>(
            parameters.Select(static parameter => parameter.Name),
            StringComparer.Ordinal);

        foreach (var parameter in parameters)
        foreach (var constraint in ConstraintReaders.GetAssignableToConstraints(parameter, attributeSymbol))
        {
            var otherParameterName = constraint.OtherParameterName;
            var isInvalidReference = string.Equals(otherParameterName, parameter.Name, StringComparison.Ordinal) ||
                                     !availableNames.Contains(otherParameterName);
            var isInvalidTypeUsage = !IsSystemType(parameter.Type) ||
                                     !parameters.Any(candidate =>
                                         string.Equals(candidate.Name, otherParameterName, StringComparison.Ordinal) &&
                                         IsSystemType(candidate.Type));

            if (!isInvalidReference && !isInvalidTypeUsage)
                continue;

            var attribute = parameter.GetAttributes().First(current =>
                SymbolEqualityComparer.Default.Equals(current.AttributeClass, attributeSymbol) &&
                current.ConstructorArguments.Length is 1 &&
                string.Equals(current.ConstructorArguments[0].Value as string, otherParameterName, StringComparison.Ordinal));

            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.InvalidAssignableToConstraintConfigurationRule,
                GetAttributeLocation(attribute, parameter),
                parameter.Name,
                otherParameterName));
        }
    }

    private static bool IsSystemType(ITypeSymbol type) =>
        type is INamedTypeSymbol namedType &&
        string.Equals(namedType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat), "global::System.Type", StringComparison.Ordinal);

    private static Location GetAttributeLocation(AttributeData attribute, ISymbol symbol) =>
        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
        ?? symbol.Locations.FirstOrDefault()
        ?? Location.None;
}
