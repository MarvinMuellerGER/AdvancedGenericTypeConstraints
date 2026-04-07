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
        if (symbols.MustMatchAssemblyNameOfAttribute is null)
            return;

        var argumentsByParameter = invocation.Arguments
            .Where(static argument => argument.Parameter is not null)
            .ToDictionary(static argument => argument.Parameter!, static argument => argument, SymbolEqualityComparer.Default);

        foreach (var parameter in invocation.TargetMethod.Parameters)
        {
            if (!argumentsByParameter.TryGetValue(parameter, out var argument))
                continue;

            var typeArgument = TryGetRepresentedType(argument.Value);
            if (typeArgument is null)
                continue;

            foreach (var assemblyConstraint in ConstraintReaders.GetAssemblyNameConstraints(
                         parameter,
                         symbols.MustMatchAssemblyNameOfAttribute))
            {
                var otherParameter = invocation.TargetMethod.Parameters.FirstOrDefault(candidate =>
                    string.Equals(candidate.Name, assemblyConstraint.OtherTypeParameterName, StringComparison.Ordinal));
                if (otherParameter is null || !argumentsByParameter.TryGetValue(otherParameter, out var otherArgument))
                    continue;

                var otherTypeArgument = TryGetRepresentedType(otherArgument.Value);
                if (otherTypeArgument is null || SymbolMatchHelpers.IsWhitelistedType(typeArgument, assemblyConstraint.AllowedTypes))
                    continue;

                var expectedAssemblyName = assemblyConstraint.Prefix +
                                           SymbolMatchHelpers.GetAssemblySimpleName(otherTypeArgument.ContainingAssembly) +
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
}
