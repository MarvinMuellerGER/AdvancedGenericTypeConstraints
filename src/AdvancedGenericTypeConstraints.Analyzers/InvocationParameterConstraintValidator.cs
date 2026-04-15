using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal static class InvocationParameterConstraintValidator
{
    public static void Validate(
        IInvocationOperation invocation,
        Action<Diagnostic> reportDiagnostic,
        ConstraintAttributeSymbols symbols,
        ConstraintCache cache)
    {
        if (symbols.MustMatchAssemblyNameOfAttribute is null &&
            symbols.MustBeOpenGenericTypeAttribute is null &&
            symbols.MustBeReferenceTypeAttribute is null &&
            symbols.MustBeAssignableToAttribute is null)
            return;

        var method = invocation.TargetMethod;
        var parameters = method.Parameters;
        var parameterIndices = cache.GetParameterIndexMap(method);
        var argumentsByOrdinal = new IArgumentOperation?[parameters.Length];
        foreach (var invocationArgument in invocation.Arguments)
            if (invocationArgument.Parameter is { Ordinal: >= 0 } parameter)
                argumentsByOrdinal[parameter.Ordinal] = invocationArgument;

        foreach (var parameter in parameters)
        {
            var parameterConstraints = cache.GetParameterConstraints(parameter);
            if (!parameterConstraints.HasAny)
                continue;

            var argument = argumentsByOrdinal[parameter.Ordinal];
            if (argument is null)
                continue;

            var typeArgument = TryGetRepresentedType(argument.Value);
            ValidateMustBeOpenGenericType(parameter, parameterConstraints, typeArgument, argument, reportDiagnostic,
                cache);
            ValidateMustBeReferenceType(parameter, parameterConstraints, typeArgument, argument, reportDiagnostic,
                cache);
            ValidateMustBeAssignableTo(
                parameter,
                parameterConstraints,
                typeArgument,
                argument,
                parameters,
                parameterIndices,
                argumentsByOrdinal,
                reportDiagnostic,
                cache);

            foreach (var assemblyConstraint in parameterConstraints.AssemblyNameConstraints)
            {
                if (!parameterIndices.TryGetValue(assemblyConstraint.OtherTypeParameterName, out var otherIndex))
                    continue;

                var otherArgument = argumentsByOrdinal[otherIndex];
                if (otherArgument is null)
                    continue;

                var otherParameter = parameters[otherIndex];

                var otherTypeArgument = TryGetRepresentedType(otherArgument.Value);
                if (typeArgument is not null &&
                    SymbolMatchHelpers.IsWhitelistedType(typeArgument, assemblyConstraint.AllowedTypes))
                    continue;

                if (typeArgument is null || otherTypeArgument is null)
                {
                    if (HasEquivalentOrStrongerAssemblyConstraint(argument, otherArgument, assemblyConstraint, cache))
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

                if (HasEquivalentOrStrongerAssemblyConstraint(argument, otherArgument, assemblyConstraint, cache))
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
                        cache))
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
        ParameterConstraintData parameterConstraints,
        ITypeSymbol? typeArgument,
        IArgumentOperation argument,
        Action<Diagnostic> reportDiagnostic,
        ConstraintCache cache)
    {
        if (!parameterConstraints.RequiresOpenGenericType)
            return;

        if (typeArgument is not null && SymbolMatchHelpers.IsOpenGenericTypeDefinition(typeArgument))
            return;

        if (IsForwardedOpenGenericTypeConstraint(argument.Value, cache))
            return;

        reportDiagnostic(Diagnostic.Create(
            ConstraintDiagnostics.MustBeOpenGenericTypeRule,
            argument.Syntax.GetLocation(),
            typeArgument is null
                ? parameter.Name
                : SymbolMatchHelpers.ToMinimalDisplayString(typeArgument)));
    }

    private static void ValidateMustBeReferenceType(
        IParameterSymbol parameter,
        ParameterConstraintData parameterConstraints,
        ITypeSymbol? typeArgument,
        IArgumentOperation argument,
        Action<Diagnostic> reportDiagnostic,
        ConstraintCache cache)
    {
        if (!parameterConstraints.RequiresReferenceType)
            return;

        if (typeArgument is not null && SymbolMatchHelpers.IsReferenceType(typeArgument))
            return;

        if (HasEquivalentReferenceTypeAttribute(argument.Value, cache))
            return;

        reportDiagnostic(Diagnostic.Create(
            ConstraintDiagnostics.MustBeReferenceTypeRule,
            argument.Syntax.GetLocation(),
            typeArgument is null
                ? parameter.Name
                : SymbolMatchHelpers.ToMinimalDisplayString(typeArgument)));
    }

    private static void ValidateMustBeAssignableTo(
        IParameterSymbol parameter,
        ParameterConstraintData parameterConstraints,
        ITypeSymbol? typeArgument,
        IArgumentOperation argument,
        ImmutableArray<IParameterSymbol> parameters,
        IReadOnlyDictionary<string, int> parameterIndices,
        IArgumentOperation?[] argumentsByOrdinal,
        Action<Diagnostic> reportDiagnostic,
        ConstraintCache cache)
    {
        foreach (var assignableConstraint in parameterConstraints.AssignableToConstraints)
        {
            if (!parameterIndices.TryGetValue(assignableConstraint.OtherParameterName, out var otherIndex))
                continue;

            var otherArgument = argumentsByOrdinal[otherIndex];
            if (otherArgument is null)
                continue;

            var otherParameter = parameters[otherIndex];

            var otherTypeArgument = TryGetRepresentedType(otherArgument.Value);
            if (typeArgument is not null && otherTypeArgument is not null)
            {
                if (SymbolMatchHelpers.IsAssignableTo(typeArgument, otherTypeArgument, cache))
                    continue;

                reportDiagnostic(Diagnostic.Create(
                    ConstraintDiagnostics.MustBeAssignableToRule,
                    argument.Syntax.GetLocation(),
                    SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                    SymbolMatchHelpers.ToMinimalDisplayString(otherTypeArgument)));

                continue;
            }

            if (HasEquivalentOrStrongerAssignableToConstraint(argument, otherArgument, cache))
                continue;

            reportDiagnostic(Diagnostic.Create(
                ConstraintDiagnostics.MustBeAssignableToRule,
                argument.Syntax.GetLocation(),
                typeArgument is null
                    ? TryGetForwardedParameter(argument.Value)?.Name ?? parameter.Name
                    : SymbolMatchHelpers.ToMinimalDisplayString(typeArgument),
                otherTypeArgument is null
                    ? TryGetForwardedParameter(otherArgument.Value)?.Name ?? otherParameter.Name
                    : SymbolMatchHelpers.ToMinimalDisplayString(otherTypeArgument)));
        }
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
        ConstraintCache cache) =>
        HasForwardedOpenGenericTypeAttribute(operation, cache);

    private static bool HasEquivalentOrStrongerAssemblyConstraint(
        IArgumentOperation argument,
        IArgumentOperation otherArgument,
        AssemblyNameConstraint requiredConstraint,
        ConstraintCache cache)
    {
        var forwardedParameter = TryGetForwardedParameter(argument.Value);
        var forwardedOtherParameter = TryGetForwardedParameter(otherArgument.Value);
        if (forwardedParameter is null || forwardedOtherParameter is null)
            return false;

        foreach (var candidate in cache.GetParameterConstraints(forwardedParameter).AssemblyNameConstraints)
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

    private static bool HasEquivalentOrStrongerAssignableToConstraint(
        IArgumentOperation argument,
        IArgumentOperation otherArgument,
        ConstraintCache cache)
    {
        var forwardedParameter = TryGetForwardedParameter(argument.Value);
        var forwardedOtherParameter = TryGetForwardedParameter(otherArgument.Value);
        if (forwardedParameter is null || forwardedOtherParameter is null)
            return false;

        return cache.GetParameterConstraints(forwardedParameter).AssignableToConstraints.Any(candidate =>
            string.Equals(candidate.OtherParameterName, forwardedOtherParameter.Name,
                StringComparison.Ordinal));
    }

    private static string FormatExpectedAssemblyName(
        string otherParameterName,
        AssemblyNameConstraint requiredConstraint) =>
        requiredConstraint.Prefix + "{AssemblyOf(" + otherParameterName + ")}" + requiredConstraint.Suffix;

    private static bool HasForwardedOpenGenericTypeAttribute(IOperation operation, ConstraintCache cache)
    {
        var forwardedParameter = TryGetForwardedParameter(operation);
        return forwardedParameter is not null &&
               cache.GetParameterConstraints(forwardedParameter).RequiresOpenGenericType;
    }

    private static bool HasEquivalentReferenceTypeAttribute(IOperation operation, ConstraintCache cache)
    {
        var forwardedParameter = TryGetForwardedParameter(operation);
        return forwardedParameter is not null &&
               cache.GetParameterConstraints(forwardedParameter).RequiresReferenceType;
    }

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
