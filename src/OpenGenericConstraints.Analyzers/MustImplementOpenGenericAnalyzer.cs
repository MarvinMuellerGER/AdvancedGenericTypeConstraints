using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace OpenGenericConstraints.Analyzers;

/// <summary>
/// Reports diagnostics for open generic constraints declared with OpenGenericConstraints attributes.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MustImplementOpenGenericAnalyzer : DiagnosticAnalyzer
{
    /// <summary>
    /// Diagnostic emitted when a type argument does not match a required open generic type definition.
    /// </summary>
    public const string MustImplementDiagnosticId = "OGC001";

    /// <summary>
    /// Diagnostic emitted when a type argument matches a forbidden open generic type definition.
    /// </summary>
    public const string MustNotImplementDiagnosticId = "OGC002";

    /// <summary>
    /// Diagnostic emitted when a type argument does not match a required open generic type definition exactly once.
    /// </summary>
    public const string MustImplementExactlyOneDiagnosticId = "OGC003";

    private static readonly DiagnosticDescriptor MustImplementRule = new(
        MustImplementDiagnosticId,
        "Type argument must implement the required open generic type",
        "Type '{0}' must implement '{1}'",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a generic type argument matches the required open generic type definition.");

    private static readonly DiagnosticDescriptor MustNotImplementRule = new(
        MustNotImplementDiagnosticId,
        "Type argument must not implement the forbidden open generic type",
        "Type '{0}' must not implement '{1}'",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a generic type argument does not match a forbidden open generic type definition.");

    private static readonly DiagnosticDescriptor MustImplementExactlyOneRule = new(
        MustImplementExactlyOneDiagnosticId,
        "Type argument must implement the required open generic type exactly once",
        "Type '{0}' must implement '{1}' exactly once",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a generic type argument matches the required open generic type definition exactly once.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [MustImplementRule, MustNotImplementRule, MustImplementExactlyOneRule];

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            var mustImplementAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(
                    "OpenGenericConstraints.MustImplementOpenGenericAttribute");
            var mustNotImplementAttributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(
                    "OpenGenericConstraints.MustNotImplementOpenGenericAttribute");

            if (mustImplementAttributeSymbol is null && mustNotImplementAttributeSymbol is null)
                return;

            startContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, mustImplementAttributeSymbol,
                    mustNotImplementAttributeSymbol),
                OperationKind.Invocation);

            startContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeGenericTypeUsage(syntaxContext, mustImplementAttributeSymbol,
                    mustNotImplementAttributeSymbol),
                SyntaxKind.GenericName);
        });
    }

    private static void AnalyzeInvocation(
        OperationAnalysisContext context,
        INamedTypeSymbol? mustImplementAttributeSymbol,
        INamedTypeSymbol? mustNotImplementAttributeSymbol)
    {
        var invocation = (IInvocationOperation)context.Operation;
        ValidateTypeArguments(
            invocation.TargetMethod.TypeParameters,
            invocation.TargetMethod.TypeArguments,
            invocation.Syntax.GetLocation(),
            context.ReportDiagnostic,
            mustImplementAttributeSymbol,
            mustNotImplementAttributeSymbol);
    }

    private static void AnalyzeGenericTypeUsage(
        SyntaxNodeAnalysisContext context,
        INamedTypeSymbol? mustImplementAttributeSymbol,
        INamedTypeSymbol? mustNotImplementAttributeSymbol)
    {
        var genericName = (GenericNameSyntax)context.Node;
        var symbol = ModelExtensions.GetSymbolInfo(context.SemanticModel, genericName, context.CancellationToken)
            .Symbol;

        if (symbol is not INamedTypeSymbol namedType || namedType.IsUnboundGenericType)
            return;

        ValidateTypeArguments(
            namedType.TypeParameters,
            namedType.TypeArguments,
            genericName.GetLocation(),
            context.ReportDiagnostic,
            mustImplementAttributeSymbol,
            mustNotImplementAttributeSymbol);
    }

    private static void ValidateTypeArguments(
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        ImmutableArray<ITypeSymbol> typeArguments,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        INamedTypeSymbol? mustImplementAttributeSymbol,
        INamedTypeSymbol? mustNotImplementAttributeSymbol)
    {
        for (var index = 0; index < typeParameters.Length && index < typeArguments.Length; index++)
        {
            var typeArgument = typeArguments[index];
            var matches = GetAllMatchingOpenGenerics(typeArgument);

            foreach (var constraint in GetMustImplementConstraints(typeParameters[index], mustImplementAttributeSymbol))
            {
                var matchCount = CountMatches(matches, constraint.OpenGenericType);
                if (constraint.ExactlyOne)
                {
                    if (matchCount is 1)
                        continue;

                    reportDiagnostic(Diagnostic.Create(
                        MustImplementExactlyOneRule,
                        location,
                        typeArgument.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                        ToOpenGenericDisplayString(constraint.OpenGenericType)));

                    continue;
                }

                if (matchCount > 0)
                    continue;

                reportDiagnostic(Diagnostic.Create(
                    MustImplementRule,
                    location,
                    typeArgument.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    ToOpenGenericDisplayString(constraint.OpenGenericType)));
            }

            foreach (var forbiddenOpenGeneric in
                     GetMustNotImplementConstraints(typeParameters[index], mustNotImplementAttributeSymbol)
                         .Where(forbiddenOpenGeneric => CountMatches(matches, forbiddenOpenGeneric) is not 0))
                reportDiagnostic(Diagnostic.Create(
                    MustNotImplementRule,
                    location,
                    typeArgument.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    ToOpenGenericDisplayString(forbiddenOpenGeneric)));
        }
    }

    private static ImmutableArray<MustImplementConstraint> GetMustImplementConstraints(
        ITypeParameterSymbol typeParameter,
        INamedTypeSymbol? attributeSymbol)
    {
        if (attributeSymbol is null)
            return [];

        var builder = ImmutableArray.CreateBuilder<MustImplementConstraint>();

        var relevantAttributes =
            typeParameter.GetAttributes().Where(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeSymbol) &&
                a.ConstructorArguments.Length is 1 or 2);

        foreach (var attribute in relevantAttributes)
        {
            var constructorArgument = attribute.ConstructorArguments[0];
            if (constructorArgument.Value is not INamedTypeSymbol openGenericType)
                continue;

            var exactlyOne = attribute.ConstructorArguments.Length is 2 &&
                             attribute.ConstructorArguments[1].Value is true;

            builder.Add(new MustImplementConstraint(openGenericType.OriginalDefinition, exactlyOne));
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<INamedTypeSymbol> GetMustNotImplementConstraints(
        ITypeParameterSymbol typeParameter,
        INamedTypeSymbol? attributeSymbol)
    {
        if (attributeSymbol is null)
            return [];

        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        var relevantAttributes =
            typeParameter.GetAttributes().Where(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeSymbol) &&
                a.ConstructorArguments.Length is 1);

        foreach (var attribute in relevantAttributes)
        {
            var constructorArgument = attribute.ConstructorArguments[0];
            if (constructorArgument.Value is not INamedTypeSymbol openGenericType)
                continue;

            builder.Add(openGenericType.OriginalDefinition);
        }

        return builder.ToImmutable();
    }

    private static ImmutableArray<INamedTypeSymbol> GetAllMatchingOpenGenerics(ITypeSymbol typeArgument)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        if (typeArgument is INamedTypeSymbol namedType)
        {
            AddIfGeneric(builder, namedType);

            for (var baseType = namedType.BaseType; baseType is not null; baseType = baseType.BaseType)
                AddIfGeneric(builder, baseType);
        }

        foreach (var implementedInterface in typeArgument.AllInterfaces)
            AddIfGeneric(builder, implementedInterface);

        return builder.ToImmutable();
    }

    private static int CountMatches(ImmutableArray<INamedTypeSymbol> matches, INamedTypeSymbol openGenericType) =>
        matches.Count(match => SymbolEqualityComparer.Default.Equals(match.OriginalDefinition, openGenericType));

    private static void AddIfGeneric(ImmutableArray<INamedTypeSymbol>.Builder builder, INamedTypeSymbol namedType)
    {
        if (!namedType.IsGenericType)
            return;

        builder.Add(namedType.OriginalDefinition);
    }

    private static string ToOpenGenericDisplayString(INamedTypeSymbol openGenericType) => openGenericType
        .ConstructUnboundGenericType().ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);

    private readonly struct MustImplementConstraint(INamedTypeSymbol openGenericType, bool exactlyOne)
    {
        public INamedTypeSymbol OpenGenericType { get; } = openGenericType;

        public bool ExactlyOne { get; } = exactlyOne;
    }
}
