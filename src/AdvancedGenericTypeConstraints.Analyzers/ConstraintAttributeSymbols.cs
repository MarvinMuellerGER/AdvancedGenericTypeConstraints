using Microsoft.CodeAnalysis;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal readonly struct ConstraintAttributeSymbols(
    INamedTypeSymbol? mustImplementAttribute,
    INamedTypeSymbol? mustNotImplementAttribute,
    INamedTypeSymbol? mustHaveAttribute,
    INamedTypeSymbol? mustMatchAssemblyNameOfAttribute)
{
    public INamedTypeSymbol? MustImplementAttribute { get; } = mustImplementAttribute;

    public INamedTypeSymbol? MustNotImplementAttribute { get; } = mustNotImplementAttribute;

    public INamedTypeSymbol? MustHaveAttribute { get; } = mustHaveAttribute;

    public INamedTypeSymbol? MustMatchAssemblyNameOfAttribute { get; } = mustMatchAssemblyNameOfAttribute;

    public bool HasAny =>
        MustImplementAttribute is not null ||
        MustNotImplementAttribute is not null ||
        MustHaveAttribute is not null ||
        MustMatchAssemblyNameOfAttribute is not null;

    public static ConstraintAttributeSymbols Create(Compilation compilation) =>
        new(
            compilation.GetTypeByMetadataName("AdvancedGenericTypeConstraints.MustImplementOpenGenericAttribute"),
            compilation.GetTypeByMetadataName("AdvancedGenericTypeConstraints.MustNotImplementOpenGenericAttribute"),
            compilation.GetTypeByMetadataName("AdvancedGenericTypeConstraints.MustHaveAttributeAttribute"),
            compilation.GetTypeByMetadataName("AdvancedGenericTypeConstraints.MustMatchAssemblyNameOfAttribute"));
}
