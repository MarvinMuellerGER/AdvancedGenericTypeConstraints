using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace AdvancedGenericTypeConstraints.Analyzers;

internal static class ConstraintDiagnostics
{
    public static readonly DiagnosticDescriptor MustImplementRule = new(
        AdvancedGenericTypeConstraintAnalyzer.MustImplementDiagnosticId,
        "Type argument must implement the required open generic type",
        "Type '{0}' must implement '{1}'",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a generic type argument matches the required open generic type definition.");

    public static readonly DiagnosticDescriptor MustNotImplementRule = new(
        AdvancedGenericTypeConstraintAnalyzer.MustNotImplementDiagnosticId,
        "Type argument must not implement the forbidden open generic type",
        "Type '{0}' must not implement '{1}'",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a generic type argument does not match a forbidden open generic type definition.");

    public static readonly DiagnosticDescriptor MustImplementExactlyOneRule = new(
        AdvancedGenericTypeConstraintAnalyzer.MustImplementExactlyOneDiagnosticId,
        "Type argument must implement the required open generic type exactly once",
        "Type '{0}' must implement '{1}' exactly once",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a generic type argument matches the required open generic type definition exactly once.");

    public static readonly DiagnosticDescriptor InvalidMustImplementConfigurationRule = new(
        AdvancedGenericTypeConstraintAnalyzer.InvalidMustImplementConfigurationDiagnosticId,
        "Only one non-interface MustImplementOpenGeneric constraint is allowed",
        "Generic parameter '{0}' can declare at most one non-interface MustImplementOpenGeneric constraint",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "A generic parameter may declare multiple MustImplementOpenGeneric constraints, but only one of them may target a non-interface type definition.");

    public static readonly DiagnosticDescriptor MustHaveAttributeRule = new(
        AdvancedGenericTypeConstraintAnalyzer.MustHaveAttributeDiagnosticId,
        "Type argument must have the required attribute",
        "Type '{0}' must be annotated with '{1}'",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a generic type argument is annotated with the required attribute.");

    public static readonly DiagnosticDescriptor MustMatchAssemblyNameRule = new(
        AdvancedGenericTypeConstraintAnalyzer.MustMatchAssemblyNameDiagnosticId,
        "Type arguments must follow the configured assembly naming convention",
        "Type '{0}' must be declared in assembly '{1}' to match type '{2}'",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a generic type argument belongs to an assembly whose simple name matches another type argument's assembly with the configured prefix and suffix.");

    public static readonly DiagnosticDescriptor InvalidAssemblyConstraintConfigurationRule = new(
        AdvancedGenericTypeConstraintAnalyzer.InvalidAssemblyConstraintConfigurationDiagnosticId,
        "The assembly naming constraint references an invalid related parameter",
        "Parameter or generic parameter '{0}' references invalid related parameter '{1}'",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "A MustMatchAssemblyNameOf constraint must reference another generic parameter or method parameter declared on the same method, or another generic parameter declared on the same type.");

    public static readonly DiagnosticDescriptor MustBeOpenGenericTypeRule = new(
        AdvancedGenericTypeConstraintAnalyzer.MustBeOpenGenericTypeDiagnosticId,
        "Type argument must be an open generic type definition",
        "Type '{0}' must be an open generic type definition",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a System.Type argument represents an open generic type definition such as IFoo<>.");

    public static readonly DiagnosticDescriptor MustBeReferenceTypeRule = new(
        AdvancedGenericTypeConstraintAnalyzer.MustBeReferenceTypeDiagnosticId,
        "Type argument must be a reference type",
        "Type '{0}' must be a reference type",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a System.Type argument represents a reference type.");

    public static readonly DiagnosticDescriptor MustBeAssignableToRule = new(
        AdvancedGenericTypeConstraintAnalyzer.MustBeAssignableToDiagnosticId,
        "Type argument must be assignable to the related type",
        "Type '{0}' must be assignable to type '{1}'",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a System.Type argument is assignable to another related System.Type argument.");

    public static readonly DiagnosticDescriptor InvalidAssignableToConstraintConfigurationRule = new(
        AdvancedGenericTypeConstraintAnalyzer.InvalidAssignableToConstraintConfigurationDiagnosticId,
        "The assignability constraint references an invalid related parameter",
        "Parameter '{0}' references invalid related parameter '{1}'",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "A MustBeAssignableTo constraint must reference another System.Type method parameter on the same method.");

    public static readonly DiagnosticDescriptor MustMatchTypeNameRule = new(
        AdvancedGenericTypeConstraintAnalyzer.MustMatchTypeNameDiagnosticId,
        "Type argument name must match the configured naming rule",
        "Type '{0}' name must start with '{1}' and end with '{2}'",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a generic type argument name matches the configured prefix and suffix.");

    public static readonly DiagnosticDescriptor InvalidTypeNameConstraintConfigurationRule = new(
        AdvancedGenericTypeConstraintAnalyzer.InvalidTypeNameConstraintConfigurationDiagnosticId,
        "The type name constraint must define at least a prefix or suffix",
        "Generic parameter '{0}' declares MustMatchTypeName without a prefix or suffix",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "A MustMatchTypeName constraint must configure at least one non-empty value.");

    public static ImmutableArray<DiagnosticDescriptor> All =>
    [
        MustImplementRule,
        MustNotImplementRule,
        MustImplementExactlyOneRule,
        InvalidMustImplementConfigurationRule,
        MustHaveAttributeRule,
        MustMatchAssemblyNameRule,
        InvalidAssemblyConstraintConfigurationRule,
        MustBeOpenGenericTypeRule,
        MustBeReferenceTypeRule,
        MustBeAssignableToRule,
        InvalidAssignableToConstraintConfigurationRule,
        MustMatchTypeNameRule,
        InvalidTypeNameConstraintConfigurationRule
    ];
}
