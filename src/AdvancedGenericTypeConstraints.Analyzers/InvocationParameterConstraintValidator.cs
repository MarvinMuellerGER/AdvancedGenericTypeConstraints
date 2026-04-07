using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal static class InvocationParameterConstraintValidator
{
    public static void Validate(
        IInvocationOperation invocation,
        Action<Diagnostic> reportDiagnostic,
        ConstraintAttributeSymbols symbols)
    {
        if (symbols.MustMatchAssemblyNameOfAttribute is null &&
            symbols.MustBeOpenGenericTypeAttribute is null)
            return;

        var argumentsByParameter = invocation.Arguments
            .Where(static argument => argument.Parameter is not null)
            .ToDictionary(static argument => argument.Parameter!, static argument => argument,
                SymbolEqualityComparer.Default);

        foreach (var parameter in invocation.TargetMethod.Parameters)
        {
            if (!argumentsByParameter.TryGetValue(parameter, out var argument))
                continue;

            var typeArgument = TryGetRepresentedType(argument.Value);
            ValidateMustBeOpenGenericType(parameter, typeArgument, argument, reportDiagnostic, symbols);

            foreach (var assemblyConstraint in ConstraintReaders.GetAssemblyNameConstraints(
                         parameter,
                         symbols.MustMatchAssemblyNameOfAttribute))
            {
                var otherParameter = invocation.TargetMethod.Parameters.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, assemblyConstraint.OtherTypeParameterName, StringComparison.Ordinal));
                if (otherParameter is null || !argumentsByParameter.TryGetValue(otherParameter, out var otherArgument))
                    continue;

                var otherTypeArgument = TryGetRepresentedType(otherArgument.Value);
                if (typeArgument is not null &&
                    SymbolMatchHelpers.IsWhitelistedType(typeArgument, assemblyConstraint.AllowedTypes))
                    continue;

                if (typeArgument is null || otherTypeArgument is null)
                {
                    if (HasEquivalentOrStrongerAssemblyConstraint(argument, otherArgument, assemblyConstraint, symbols))
                        continue;

                    var forwardedParameter = TryGetForwardedParameter(argument.Value);
                    var forwardedOtherParameter = TryGetForwardedParameter(otherArgument.Value);
                    reportDiagnostic(Diagnostic.Create(
                        ConstraintDiagnostics.MustMatchAssemblyNameRule,
                        argument.Syntax.GetLocation(),
                        typeArgument is null
                            ? forwardedParameter?.Name ?? parameter.Name
                            : SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                        otherTypeArgument is null
                            ? FormatExpectedAssemblyName(
                                forwardedOtherParameter?.Name ?? otherParameter.Name,
                                assemblyConstraint)
                            : assemblyConstraint.Prefix +
                              SymbolMatchHelpers.GetAssemblySimpleName(otherTypeArgument.ContainingAssembly) +
                              assemblyConstraint.Suffix,
                        otherTypeArgument is null
                            ? forwardedOtherParameter?.Name ?? otherParameter.Name
                            : SymbolMatchHelpers.ToMinimalDisplayString(otherTypeArgument)));

                    continue;
                }

                if (HasEquivalentOrStrongerAssemblyConstraint(argument, otherArgument, assemblyConstraint, symbols))
                    continue;

                var expectedAssemblyName = assemblyConstraint.Prefix +
                                           SymbolMatchHelpers.GetAssemblySimpleName(
                                               otherTypeArgument.ContainingAssembly) +
                                           assemblyConstraint.Suffix;
                var actualAssemblyName = SymbolMatchHelpers.GetAssemblySimpleName(typeArgument.ContainingAssembly);

                if (string.Equals(expectedAssemblyName, actualAssemblyName, StringComparison.Ordinal) ||
                    TypeArgumentConstraintValidator.HasEquivalentOrStrongerAssemblyConstraint(
                        typeArgument,
                        otherTypeArgument,
                        assemblyConstraint,
                        symbols))
                    continue;

                reportDiagnostic(Diagnostic.Create(
                    ConstraintDiagnostics.MustMatchAssemblyNameRule,
                    argument.Syntax.GetLocation(),
                    SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                    expectedAssemblyName,
                    SymbolMatchHelpers.ToMinimalDisplayString(otherTypeArgument)));
            }
        }
    }

    private static void ValidateMustBeOpenGenericType(
        IParameterSymbol parameter,
        ITypeSymbol? typeArgument,
        IArgumentOperation argument,
        Action<Diagnostic> reportDiagnostic,
        ConstraintAttributeSymbols symbols)
    {
        if (symbols.MustBeOpenGenericTypeAttribute is null)
            return;

        if (!parameter.GetAttributes().Any(attribute =>
                SymbolEqualityComparer.Default.Equals(attribute.AttributeClass,
                    symbols.MustBeOpenGenericTypeAttribute)))
            return;

        if (typeArgument is not null && SymbolMatchHelpers.IsOpenGenericTypeDefinition(typeArgument))
            return;

        if (IsForwardedOpenGenericTypeConstraint(argument.Value, symbols))
            return;

        reportDiagnostic(Diagnostic.Create(
            ConstraintDiagnostics.MustBeOpenGenericTypeRule,
            argument.Syntax.GetLocation(),
            typeArgument is null
                ? parameter.Name
                : SymbolMatchHelpers.ToMinimalDisplayString(typeArgument)));
    }

    private static ITypeSymbol? TryGetRepresentedType(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
            operation = conversion.Operand;

        return operation switch
        {
            ITypeOfOperation typeOfOperation => typeOfOperation.TypeOperand,
            _ => null
        };
    }

    private static bool IsForwardedOpenGenericTypeConstraint(
        IOperation operation,
        ConstraintAttributeSymbols symbols)
    {
        if (symbols.MustBeOpenGenericTypeAttribute is null)
            return false;

        var forwardedParameter = TryGetForwardedParameter(operation);
        return forwardedParameter is not null &&
               forwardedParameter.GetAttributes().Any(attribute =>
                   SymbolEqualityComparer.Default.Equals(attribute.AttributeClass,
                       symbols.MustBeOpenGenericTypeAttribute));
    }

    private static bool HasEquivalentOrStrongerAssemblyConstraint(
        IArgumentOperation argument,
        IArgumentOperation otherArgument,
        AssemblyNameConstraint requiredConstraint,
        ConstraintAttributeSymbols symbols)
    {
        if (symbols.MustMatchAssemblyNameOfAttribute is null)
            return false;

        var forwardedParameter = TryGetForwardedParameter(argument.Value);
        var forwardedOtherParameter = TryGetForwardedParameter(otherArgument.Value);
        if (forwardedParameter is null || forwardedOtherParameter is null)
            return false;

        foreach (var candidate in ConstraintReaders.GetAssemblyNameConstraints(
                     forwardedParameter,
                     symbols.MustMatchAssemblyNameOfAttribute))
        {
            if (!string.Equals(candidate.Prefix, requiredConstraint.Prefix, StringComparison.Ordinal) ||
                !string.Equals(candidate.Suffix, requiredConstraint.Suffix, StringComparison.Ordinal) ||
                !IsAllowedTypeSubset(candidate.AllowedTypes, requiredConstraint.AllowedTypes))
                continue;

            if (string.Equals(candidate.OtherTypeParameterName, forwardedOtherParameter.Name, StringComparison.Ordinal))
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

    private static string FormatExpectedAssemblyName(
        string otherParameterName,
        AssemblyNameConstraint requiredConstraint) =>
        requiredConstraint.Prefix + "{AssemblyOf(" + otherParameterName + ")}" + requiredConstraint.Suffix;

    private static IParameterSymbol? TryGetForwardedParameter(IOperation operation)
    {
        while (operation is IConversionOperation { IsImplicit: true } conversion)
            operation = conversion.Operand;

        return operation switch
        {
            IParameterReferenceOperation parameterReference => parameterReference.Parameter,
            _ => null
        };
    }
}
