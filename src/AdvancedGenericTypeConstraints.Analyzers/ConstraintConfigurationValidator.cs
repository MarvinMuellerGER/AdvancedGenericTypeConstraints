using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal static class ConstraintConfigurationValidator
{
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

    private static void ValidateMustImplementConstraintConfiguration(
        ITypeParameterSymbol typeParameter,
        INamedTypeSymbol attributeSymbol,
        Action<Diagnostic> reportDiagnostic)
    {
        var nonInterfaceAttributes = typeParameter.GetAttributes()
            .Where(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, attributeSymbol) &&
                attribute.ConstructorArguments.Length is 1 or 2 &&
                attribute.ConstructorArguments[0].Value is INamedTypeSymbol openGenericType &&
                openGenericType.TypeKind is not TypeKind.Interface)
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

    private static Location GetAttributeLocation(AttributeData attribute, ITypeParameterSymbol typeParameter) =>
        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()
        ?? typeParameter.Locations.FirstOrDefault()
        ?? Location.None;
}
