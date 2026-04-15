using Microsoft.CodeAnalysis;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal readonly struct ConstraintAttributeSymbols(
    INamedTypeSymbol? mustImplementAttribute,
    INamedTypeSymbol? mustNotImplementAttribute,
    INamedTypeSymbol? mustHaveAttribute,
    INamedTypeSymbol? mustMatchAssemblyNameOfAttribute,
    INamedTypeSymbol? mustMatchTypeNameAttribute,
    INamedTypeSymbol? mustBeOpenGenericTypeAttribute,
    INamedTypeSymbol? mustBeReferenceTypeAttribute,
    INamedTypeSymbol? mustBeAssignableToAttribute)
{
    public INamedTypeSymbol? MustImplementAttribute { get; } = mustImplementAttribute;

    public INamedTypeSymbol? MustNotImplementAttribute { get; } = mustNotImplementAttribute;

    public INamedTypeSymbol? MustHaveAttribute { get; } = mustHaveAttribute;

    public INamedTypeSymbol? MustMatchAssemblyNameOfAttribute { get; } = mustMatchAssemblyNameOfAttribute;

    public INamedTypeSymbol? MustMatchTypeNameAttribute { get; } = mustMatchTypeNameAttribute;

    public INamedTypeSymbol? MustBeOpenGenericTypeAttribute { get; } = mustBeOpenGenericTypeAttribute;

    public INamedTypeSymbol? MustBeReferenceTypeAttribute { get; } = mustBeReferenceTypeAttribute;

    public INamedTypeSymbol? MustBeAssignableToAttribute { get; } = mustBeAssignableToAttribute;

    public bool HasAny =>
        MustImplementAttribute is not null ||
        MustNotImplementAttribute is not null ||
        MustHaveAttribute is not null ||
        MustMatchAssemblyNameOfAttribute is not null ||
        MustMatchTypeNameAttribute is not null ||
        MustBeOpenGenericTypeAttribute is not null ||
        MustBeReferenceTypeAttribute is not null ||
        MustBeAssignableToAttribute is not null;

    public static ConstraintAttributeSymbols Create(Compilation compilation) =>
        new(
            compilation.GetTypeByMetadataName("AdvancedGenericTypeConstraints.MustImplementOpenGenericAttribute"),
            compilation.GetTypeByMetadataName("AdvancedGenericTypeConstraints.MustNotImplementOpenGenericAttribute"),
            compilation.GetTypeByMetadataName("AdvancedGenericTypeConstraints.MustHaveAttributeAttribute"),
            compilation.GetTypeByMetadataName("AdvancedGenericTypeConstraints.MustMatchAssemblyNameOfAttribute"),
            compilation.GetTypeByMetadataName("AdvancedGenericTypeConstraints.MustMatchTypeNameAttribute"),
            compilation.GetTypeByMetadataName("AdvancedGenericTypeConstraints.MustBeOpenGenericTypeAttribute"),
            compilation.GetTypeByMetadataName("AdvancedGenericTypeConstraints.MustBeReferenceTypeAttribute"),
            compilation.GetTypeByMetadataName("AdvancedGenericTypeConstraints.MustBeAssignableToAttribute"));
}
