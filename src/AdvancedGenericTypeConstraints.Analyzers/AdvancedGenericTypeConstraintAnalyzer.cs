using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace AdvancedGenericTypeConstraints.Analyzers;

/// <summary>
/// Reports diagnostics for generic type checks declared with <c>AdvancedGenericTypeConstraints</c> attributes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class AdvancedGenericTypeConstraintAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Diagnostic emitted when a type argument does not match a required open generic type definition.
    /// </summary>
    public const string MustImplementDiagnosticId = "AGTC001";

    /// <summary>
    /// Diagnostic emitted when a type argument matches a forbidden open generic type definition.
    /// </summary>
    public const string MustNotImplementDiagnosticId = "AGTC002";

    /// <summary>
    /// Diagnostic emitted when a type argument does not match a required open generic type definition exactly once.
    /// </summary>
    public const string MustImplementExactlyOneDiagnosticId = "AGTC003";

    /// <summary>
    /// Diagnostic emitted when a generic parameter declares multiple non-interface MustImplementOpenGeneric constraints.
    /// </summary>
    public const string InvalidMustImplementConfigurationDiagnosticId = "AGTC004";

    /// <summary>
    /// Diagnostic emitted when a type argument is missing a required attribute.
    /// </summary>
    public const string MustHaveAttributeDiagnosticId = "AGTC005";

    /// <summary>
    /// Diagnostic emitted when two type arguments do not satisfy the configured assembly naming rule.
    /// </summary>
    public const string MustMatchAssemblyNameDiagnosticId = "AGTC006";

    /// <summary>
    /// Diagnostic emitted when a MustMatchAssemblyNameOf constraint references an invalid generic parameter.
    /// </summary>
    public const string InvalidAssemblyConstraintConfigurationDiagnosticId = "AGTC007";

    /// <summary>
    /// Diagnostic emitted when a <see cref="Type"/> argument is not an open generic type definition.
    /// </summary>
    public const string MustBeOpenGenericTypeDiagnosticId = "AGTC008";

    /// <summary>
    /// Diagnostic emitted when a <see cref="Type"/> argument is not a reference type.
    /// </summary>
    public const string MustBeReferenceTypeDiagnosticId = "AGTC009";

    /// <summary>
    /// Diagnostic emitted when a <see cref="Type"/> argument is not assignable to another related type.
    /// </summary>
    public const string MustBeAssignableToDiagnosticId = "AGTC010";

    /// <summary>
    /// Diagnostic emitted when a MustBeAssignableTo constraint references an invalid related parameter.
    /// </summary>
    public const string InvalidAssignableToConstraintConfigurationDiagnosticId = "AGTC011";

    /// <summary>
    /// Diagnostic emitted when a type argument name does not match the configured prefix and/or suffix.
    /// </summary>
    public const string MustMatchTypeNameDiagnosticId = "AGTC012";

    /// <summary>
    /// Diagnostic emitted when a MustMatchTypeName constraint is configured without a prefix and suffix.
    /// </summary>
    public const string InvalidTypeNameConstraintConfigurationDiagnosticId = "AGTC013";

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ConstraintDiagnostics.All;

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            var symbols = ConstraintAttributeSymbols.Create(startContext.Compilation);
            if (!symbols.HasAny)
                return;

            startContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, symbols),
                OperationKind.Invocation);

            startContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeGenericTypeUsage(syntaxContext, symbols),
                SyntaxKind.GenericName);

            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeMethod(symbolContext, symbols),
                SymbolKind.Method);

            startContext.RegisterSymbolAction(
                symbolContext => AnalyzeNamedType(symbolContext, symbols),
                SymbolKind.NamedType);
        });
    }

    private static void AnalyzeMethod(SymbolAnalysisContext context, ConstraintAttributeSymbols symbols)
    {
        var method = (IMethodSymbol)context.Symbol;
        ConstraintConfigurationValidator.Validate(method, symbols, context.ReportDiagnostic);
    }

    private static void AnalyzeNamedType(SymbolAnalysisContext context, ConstraintAttributeSymbols symbols)
    {
        var namedType = (INamedTypeSymbol)context.Symbol;
        ConstraintConfigurationValidator.Validate(namedType.TypeParameters, symbols, context.ReportDiagnostic);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, ConstraintAttributeSymbols symbols)
    {
        var invocation = (IInvocationOperation)context.Operation;
        TypeArgumentConstraintValidator.Validate(
            invocation.TargetMethod.TypeParameters,
            invocation.TargetMethod.TypeArguments,
            invocation.Syntax.GetLocation(),
            context.ReportDiagnostic,
            symbols);

        InvocationParameterConstraintValidator.Validate(
            invocation,
            context.ReportDiagnostic,
            symbols);
    }

    private static void AnalyzeGenericTypeUsage(SyntaxNodeAnalysisContext context, ConstraintAttributeSymbols symbols)
    {
        var genericName = (GenericNameSyntax)context.Node;
        var symbol = ModelExtensions.GetSymbolInfo(context.SemanticModel, genericName, context.CancellationToken)
            .Symbol;

        if (symbol is not INamedTypeSymbol namedType || namedType.IsUnboundGenericType)
            return;

        TypeArgumentConstraintValidator.Validate(
            namedType.TypeParameters,
            namedType.TypeArguments,
            genericName.GetLocation(),
            context.ReportDiagnostic,
            symbols);
    }
}
