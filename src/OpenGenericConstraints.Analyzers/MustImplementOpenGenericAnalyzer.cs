using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace OpenGenericConstraints.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class MustImplementOpenGenericAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "OGC001";

    private static readonly DiagnosticDescriptor Rule = new(
        DiagnosticId,
        "Type argument must implement the required open generic interface",
        "Type '{0}' must implement '{1}'",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Ensures that a generic type argument implements an interface matching the required open generic definition.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [Rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        context.RegisterCompilationStartAction(static startContext =>
        {
            var attributeSymbol =
                startContext.Compilation.GetTypeByMetadataName(
                    "OpenGenericConstraints.MustImplementOpenGenericAttribute");

            if (attributeSymbol is null) return;

            startContext.RegisterOperationAction(
                operationContext => AnalyzeInvocation(operationContext, attributeSymbol),
                OperationKind.Invocation);

            startContext.RegisterSyntaxNodeAction(
                syntaxContext => AnalyzeGenericTypeUsage(syntaxContext, attributeSymbol),
                SyntaxKind.GenericName);
        });
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context, INamedTypeSymbol attributeSymbol)
    {
        var invocation = (IInvocationOperation)context.Operation;
        ValidateTypeArguments(
            invocation.TargetMethod.TypeParameters,
            invocation.TargetMethod.TypeArguments,
            invocation.Syntax.GetLocation(),
            context.ReportDiagnostic,
            attributeSymbol);
    }

    private static void AnalyzeGenericTypeUsage(SyntaxNodeAnalysisContext context, INamedTypeSymbol attributeSymbol)
    {
        var genericName = (GenericNameSyntax)context.Node;
        var symbol = ModelExtensions.GetSymbolInfo(context.SemanticModel, genericName, context.CancellationToken)
            .Symbol;

        if (symbol is not INamedTypeSymbol namedType || namedType.IsUnboundGenericType) return;

        ValidateTypeArguments(
            namedType.TypeParameters,
            namedType.TypeArguments,
            genericName.GetLocation(),
            context.ReportDiagnostic,
            attributeSymbol);
    }

    private static void ValidateTypeArguments(
        ImmutableArray<ITypeParameterSymbol> typeParameters,
        ImmutableArray<ITypeSymbol> typeArguments,
        Location location,
        Action<Diagnostic> reportDiagnostic,
        INamedTypeSymbol attributeSymbol)
    {
        for (var index = 0; index < typeParameters.Length && index < typeArguments.Length; index++)
        {
            var requiredOpenGenerics = GetRequiredOpenGenerics(typeParameters[index], attributeSymbol);
            if (requiredOpenGenerics.IsDefaultOrEmpty) continue;

            var typeArgument = typeArguments[index];
            var openGenerics =
                requiredOpenGenerics.Where(openGeneric => !ImplementsOpenGeneric(typeArgument, openGeneric));

            foreach (var openGeneric in openGenerics)
                reportDiagnostic(Diagnostic.Create(
                    Rule,
                    location,
                    typeArgument.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                    ToOpenGenericDisplayString(openGeneric)));
        }
    }

    private static ImmutableArray<INamedTypeSymbol> GetRequiredOpenGenerics(ITypeParameterSymbol typeParameter,
        INamedTypeSymbol attributeSymbol)
    {
        var builder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

        var relevantAttributes =
            typeParameter.GetAttributes().Where(a =>
                SymbolEqualityComparer.Default.Equals(a.AttributeClass, attributeSymbol) &&
                a.ConstructorArguments.Length is 1);

        foreach (var attribute in relevantAttributes)
        {
            var constructorArgument = attribute.ConstructorArguments[0];
            if (constructorArgument.Value is not INamedTypeSymbol openGenericType) continue;

            builder.Add(openGenericType.OriginalDefinition);
        }

        return builder.ToImmutable();
    }

    private static bool ImplementsOpenGeneric(ITypeSymbol typeArgument, INamedTypeSymbol openGenericType)
    {
        if (typeArgument is INamedTypeSymbol { IsGenericType: true } namedType &&
            SymbolEqualityComparer.Default.Equals(namedType.OriginalDefinition, openGenericType))
            return true;

        return typeArgument.AllInterfaces.Any(i =>
            i.IsGenericType &&
            SymbolEqualityComparer.Default.Equals(i.OriginalDefinition, openGenericType));
    }

    private static string ToOpenGenericDisplayString(INamedTypeSymbol openGenericType) => openGenericType
        .ConstructUnboundGenericType().ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
}
