using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OpenGenericConstraints.Analyzers.Tests;

public class MustImplementOpenGenericAnalyzerTests
{
    [Fact]
    public async Task Reports_No_Diagnostic_When_Type_Implements_Required_Open_Generic()
    {
        const string source = """
            using System;
            using OpenGenericConstraints;

            public interface IHandleMessages<T> { }

            public interface IFeatureRegistry
            {
                void RegisterMessageHandler<[MustImplementOpenGeneric(typeof(IHandleMessages<>))] TMessageHandler>();
            }

            public sealed class MyMessage { }

            public sealed class MyHandler : IHandleMessages<MyMessage> { }

            public static class Demo
            {
                public static void Run(IFeatureRegistry registry)
                {
                    registry.RegisterMessageHandler<MyHandler>();
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task Reports_Diagnostic_When_Type_Does_Not_Implement_Required_Open_Generic()
    {
        const string source = """
            using System;
            using OpenGenericConstraints;

            public interface IHandleMessages<T> { }

            public interface IFeatureRegistry
            {
                void RegisterMessageHandler<[MustImplementOpenGeneric(typeof(IHandleMessages<>))] TMessageHandler>();
            }

            public sealed class MyHandler { }

            public static class Demo
            {
                public static void Run(IFeatureRegistry registry)
                {
                    registry.RegisterMessageHandler<MyHandler>();
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(MustImplementOpenGenericAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must implement 'IHandleMessages<>'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Reports_Diagnostic_When_Type_Implements_Different_Open_Generic()
    {
        const string source = """
            using System;
            using OpenGenericConstraints;

            public interface IHandleMessages<T> { }
            public interface IOtherMessages<T> { }

            public interface IFeatureRegistry
            {
                void RegisterMessageHandler<[MustImplementOpenGeneric(typeof(IHandleMessages<>))] TMessageHandler>();
            }

            public sealed class MyMessage { }

            public sealed class MyHandler : IOtherMessages<MyMessage> { }

            public static class Demo
            {
                public static void Run(IFeatureRegistry registry)
                {
                    registry.RegisterMessageHandler<MyHandler>();
                }
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(MustImplementOpenGenericAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must implement 'IHandleMessages<>'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task Reports_Diagnostic_For_Generic_Type_Usage()
    {
        const string source = """
            using OpenGenericConstraints;

            public interface IHandleMessages<T> { }

            public sealed class HandlerRegistry<[MustImplementOpenGeneric(typeof(IHandleMessages<>))] THandler>
            {
            }

            public sealed class MyHandler { }

            public sealed class Demo
            {
                private readonly HandlerRegistry<MyHandler> _registry = new();
            }
            """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(MustImplementOpenGenericAnalyzer.DiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must implement 'IHandleMessages<>'", diagnostic.GetMessage());
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            assemblyName: "AnalyzerTests",
            syntaxTrees: [syntaxTree],
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationErrors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            compilationErrors.Length is 0,
            "Test compilation failed:\n" + string.Join(Environment.NewLine, compilationErrors.Select(static diagnostic => diagnostic.ToString())));

        var analyzer = new MustImplementOpenGenericAnalyzer();
        var diagnostics = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();

        return diagnostics.Sort(static (left, right) => left.Location.SourceSpan.Start.CompareTo(right.Location.SourceSpan.Start));
    }

    private static MetadataReference[] GetMetadataReferences()
    {
        var frameworkReferences = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(static path => MetadataReference.CreateFromFile(path));

        return frameworkReferences
            .Append(MetadataReference.CreateFromFile(typeof(MustImplementOpenGenericAttribute).Assembly.Location))
            .DistinctBy(static reference => reference.Display, StringComparer.OrdinalIgnoreCase)
            .ToArray<MetadataReference>();
    }
}
