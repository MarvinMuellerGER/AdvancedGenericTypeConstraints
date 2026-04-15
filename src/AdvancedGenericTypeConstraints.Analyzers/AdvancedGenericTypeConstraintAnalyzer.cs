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

            var cache = new ConstraintCache(startContext.Compilation, symbols);

            startContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, symbols, cache),
                OperationKind.Invocation);

            startContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeGenericTypeUsage(syntaxContext, cache),
                SyntaxKind.GenericName);

            startContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeMethodDeclaration(syntaxContext, symbols, cache),
                SyntaxKind.MethodDeclaration,
                SyntaxKind.ConstructorDeclaration,
                SyntaxKind.OperatorDeclaration,
                SyntaxKind.ConversionOperatorDeclaration);

            startContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeDelegateDeclaration(syntaxContext, symbols, cache),
                SyntaxKind.DelegateDeclaration);

            startContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeNamedTypeDeclaration(syntaxContext, symbols, cache),
                SyntaxKind.ClassDeclaration,
                SyntaxKind.StructDeclaration,
                SyntaxKind.InterfaceDeclaration,
                SyntaxKind.RecordDeclaration,
                SyntaxKind.RecordStructDeclaration);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        ConstraintAttributeSymbols symbols,
        ConstraintCache cache)
    {
        var invocation = (IInvocationOperation)context.Operation;
        if (!cache.HasRelevantInvocationConstraints(invocation.TargetMethod))
            return;

        TypeArgumentConstraintValidator.Validate(
            invocation.TargetMethod.TypeParameters,
            invocation.TargetMethod.TypeArguments,
            invocation.Syntax.GetLocation(),
            context.ReportDiagnostic,
            cache);

        InvocationParameterConstraintValidator.Validate(
            invocation,
            context.ReportDiagnostic,
            symbols,
            cache);
    }

    private static void AnalyzeGenericTypeUsage(
        SyntaxNodeAnalysisContext context,
        ConstraintCache cache)
    {
        var genericName = (GenericNameSyntax)context.Node;
        if (!IsPotentialGenericTypeUsage(genericName))
            return;

        var symbol = ModelExtensions.GetSymbolInfo(context.SemanticModel, genericName, context.CancellationToken)
            .Symbol;

        if (symbol is not INamedTypeSymbol namedType || namedType.IsUnboundGenericType)
            return;

        if (!cache.HasRelevantTypeArgumentConstraints(namedType))
            return;

        TypeArgumentConstraintValidator.Validate(
            namedType.TypeParameters,
            namedType.TypeArguments,
            genericName.GetLocation(),
            context.ReportDiagnostic,
            cache);
    }

    private static bool IsPotentialGenericTypeUsage(GenericNameSyntax genericName)
    {
        if (genericName.Parent is TypeArgumentListSyntax)
            return false;

        if (genericName.Parent is AttributeSyntax)
            return false;

        if (genericName.Parent is NameMemberCrefSyntax or QualifiedCrefSyntax)
            return false;

        return genericName.Parent switch
        {
            InvocationExpressionSyntax { Expression: var expression } => !ReferenceEquals(expression, genericName),
            MemberAccessExpressionSyntax memberAccess when ReferenceEquals(memberAccess.Name, genericName) &&
                                                        memberAccess.Parent is InvocationExpressionSyntax
                => false,
            MemberBindingExpressionSyntax memberBinding when ReferenceEquals(memberBinding.Name, genericName) &&
                                                            memberBinding.Parent?.Parent is ConditionalAccessExpressionSyntax
                                                            {
                                                                Parent: InvocationExpressionSyntax
                                                            }
                => false,
            _ => true
        };
    }

    private static void AnalyzeMethodDeclaration(
        SyntaxNodeAnalysisContext context,
        ConstraintAttributeSymbols symbols,
        ConstraintCache cache)
    {
        if (!HasRelevantMethodConfigurationAttributes(context.Node))
            return;

        if (context.SemanticModel.GetDeclaredSymbol(context.Node, context.CancellationToken) is not IMethodSymbol method)
            return;

        ConstraintConfigurationValidator.Validate(method, symbols, cache, context.ReportDiagnostic);
    }

    private static void AnalyzeDelegateDeclaration(
        SyntaxNodeAnalysisContext context,
        ConstraintAttributeSymbols symbols,
        ConstraintCache cache)
    {
        var delegateDeclaration = (DelegateDeclarationSyntax)context.Node;
        if (!HasRelevantTypeParameterConfigurationAttributes(delegateDeclaration.TypeParameterList))
            return;

        if (context.SemanticModel.GetDeclaredSymbol(delegateDeclaration, context.CancellationToken) is not INamedTypeSymbol namedType)
            return;

        ConstraintConfigurationValidator.Validate(namedType.TypeParameters, symbols, cache, context.ReportDiagnostic);
    }

    private static void AnalyzeNamedTypeDeclaration(
        SyntaxNodeAnalysisContext context,
        ConstraintAttributeSymbols symbols,
        ConstraintCache cache)
    {
        var typeDeclaration = (TypeDeclarationSyntax)context.Node;
        if (!HasRelevantTypeParameterConfigurationAttributes(typeDeclaration.TypeParameterList))
            return;

        if (context.SemanticModel.GetDeclaredSymbol(typeDeclaration, context.CancellationToken) is not INamedTypeSymbol namedType)
            return;

        ConstraintConfigurationValidator.Validate(namedType.TypeParameters, symbols, cache, context.ReportDiagnostic);
    }

    private static bool HasRelevantMethodConfigurationAttributes(SyntaxNode node)
    {
        return node switch
        {
            BaseMethodDeclarationSyntax methodDeclaration =>
                HasRelevantTypeParameterConfigurationAttributes(methodDeclaration switch
                {
                    MethodDeclarationSyntax method => method.TypeParameterList,
                    _ => null
                }) ||
                HasRelevantParameterConfigurationAttributes(methodDeclaration.ParameterList),
            _ => false
        };
    }

    private static bool HasRelevantTypeParameterConfigurationAttributes(TypeParameterListSyntax? typeParameterList)
    {
        if (typeParameterList is null)
            return false;

        foreach (var typeParameter in typeParameterList.Parameters)
        foreach (var attributeList in typeParameter.AttributeLists)
        foreach (var attribute in attributeList.Attributes)
        {
            if (IsRelevantTypeParameterConfigurationAttribute(attribute))
                return true;
        }

        return false;
    }

    private static bool HasRelevantParameterConfigurationAttributes(ParameterListSyntax parameterList)
    {
        foreach (var parameter in parameterList.Parameters)
        foreach (var attributeList in parameter.AttributeLists)
        foreach (var attribute in attributeList.Attributes)
        {
            if (IsRelevantParameterConfigurationAttribute(attribute))
                return true;
        }

        return false;
    }

    private static bool IsRelevantTypeParameterConfigurationAttribute(AttributeSyntax attribute)
    {
        var name = GetUnqualifiedAttributeName(attribute.Name);
        return name is "MustImplementOpenGeneric" or "MustImplementOpenGenericAttribute"
            or "MustMatchAssemblyNameOf" or "MustMatchAssemblyNameOfAttribute"
            or "MustMatchTypeName" or "MustMatchTypeNameAttribute";
    }

    private static bool IsRelevantParameterConfigurationAttribute(AttributeSyntax attribute)
    {
        var name = GetUnqualifiedAttributeName(attribute.Name);
        return name is "MustMatchAssemblyNameOf" or "MustMatchAssemblyNameOfAttribute"
            or "MustBeOpenGenericType" or "MustBeOpenGenericTypeAttribute"
            or "MustBeReferenceType" or "MustBeReferenceTypeAttribute"
            or "MustBeAssignableTo" or "MustBeAssignableToAttribute";
    }

    private static string GetUnqualifiedAttributeName(NameSyntax nameSyntax) =>
        nameSyntax switch
        {
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            QualifiedNameSyntax qualifiedName => GetUnqualifiedAttributeName(qualifiedName.Right),
            AliasQualifiedNameSyntax aliasQualifiedName => aliasQualifiedName.Name.Identifier.ValueText,
            GenericNameSyntax genericName => genericName.Identifier.ValueText,
            _ => nameSyntax.ToString()
        };
}
