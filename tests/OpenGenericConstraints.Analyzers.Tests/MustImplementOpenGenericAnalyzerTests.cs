using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace OpenGenericConstraints.Analyzers.Tests;

public class MustImplementOpenGenericAnalyzerTests
{
    [Fact]
    public async Task ReportsNoDiagnostic_When_TypeImplementsRequiredOpenGeneric()
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
    public async Task ReportsDiagnostic_When_TypeDoesNotImplementRequiredOpenGeneric()
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
        Assert.Equal(MustImplementOpenGenericAnalyzer.MustImplementDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must implement 'IHandleMessages<>'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsDiagnostic_When_TypeImplementsDifferentOpenGeneric()
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
        Assert.Equal(MustImplementOpenGenericAnalyzer.MustImplementDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must implement 'IHandleMessages<>'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsDiagnostic_When_GenericTypeUsageViolatesConstraint()
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
        Assert.Equal(MustImplementOpenGenericAnalyzer.MustImplementDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must implement 'IHandleMessages<>'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_GenericBaseTypeMatches()
    {
        const string source = """
                              using OpenGenericConstraints;

                              public class MessageHandler<TMessage>
                              {
                              }

                              public sealed class MyMessage
                              {
                              }

                              public sealed class MyHandler : MessageHandler<MyMessage>
                              {
                              }

                              public interface IFeatureRegistry
                              {
                                  void Register<[MustImplementOpenGeneric(typeof(MessageHandler<>))] THandler>();
                              }

                              public static class Demo
                              {
                                  public static void Run(IFeatureRegistry registry)
                                  {
                                      registry.Register<MyHandler>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsDiagnostic_When_TypeMustNotImplementOpenGeneric()
    {
        const string source = """
                              using OpenGenericConstraints;

                              public interface IHandleMessages<T> { }

                              public sealed class MyMessage { }

                              public sealed class MyHandler : IHandleMessages<MyMessage> { }

                              public interface IFeatureRegistry
                              {
                                  void Register<[MustNotImplementOpenGeneric(typeof(IHandleMessages<>))] THandler>();
                              }

                              public static class Demo
                              {
                                  public static void Run(IFeatureRegistry registry)
                                  {
                                      registry.Register<MyHandler>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(MustImplementOpenGenericAnalyzer.MustNotImplementDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must not implement 'IHandleMessages<>'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsDiagnostic_When_ExactlyOneMatchIsRequiredButNoneExist()
    {
        const string source = """
                              using OpenGenericConstraints;

                              public interface IHandleMessages<T> { }

                              public sealed class MyHandler { }

                              public interface IFeatureRegistry
                              {
                                  void Register<[MustImplementOpenGeneric(typeof(IHandleMessages<>), true)] THandler>();
                              }

                              public static class Demo
                              {
                                  public static void Run(IFeatureRegistry registry)
                                  {
                                      registry.Register<MyHandler>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(MustImplementOpenGenericAnalyzer.MustImplementExactlyOneDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must implement 'IHandleMessages<>' exactly once", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsDiagnostic_When_ExactlyOneMatchIsRequiredButMultipleExist()
    {
        const string source = """
                              using OpenGenericConstraints;

                              public interface IHandleMessages<T> { }

                              public sealed class MessageA { }
                              public sealed class MessageB { }

                              public sealed class MyHandler : IHandleMessages<MessageA>, IHandleMessages<MessageB> { }

                              public interface IFeatureRegistry
                              {
                                  void Register<[MustImplementOpenGeneric(typeof(IHandleMessages<>), true)] THandler>();
                              }

                              public static class Demo
                              {
                                  public static void Run(IFeatureRegistry registry)
                                  {
                                      registry.Register<MyHandler>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(MustImplementOpenGenericAnalyzer.MustImplementExactlyOneDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must implement 'IHandleMessages<>' exactly once", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_ExactlyOneMatchIsRequiredAndOneExists()
    {
        const string source = """
                              using OpenGenericConstraints;

                              public interface IHandleMessages<T> { }

                              public sealed class MyMessage { }

                              public sealed class MyHandler : IHandleMessages<MyMessage> { }

                              public interface IFeatureRegistry
                              {
                                  void Register<[MustImplementOpenGeneric(typeof(IHandleMessages<>), true)] THandler>();
                              }

                              public static class Demo
                              {
                                  public static void Run(IFeatureRegistry registry)
                                  {
                                      registry.Register<MyHandler>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

        var references = GetMetadataReferences();

        var compilation = CSharpCompilation.Create(
            "AnalyzerTests",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var compilationErrors = compilation.GetDiagnostics()
            .Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)
            .ToArray();

        Assert.True(
            compilationErrors.Length is 0,
            "Test compilation failed:\n" + string.Join(Environment.NewLine,
                compilationErrors.Select(static diagnostic => diagnostic.ToString())));

        var analyzer = new MustImplementOpenGenericAnalyzer();
        var diagnostics = await compilation.WithAnalyzers([analyzer]).GetAnalyzerDiagnosticsAsync();

        return diagnostics.Sort(static (left, right) =>
            left.Location.SourceSpan.Start.CompareTo(right.Location.SourceSpan.Start));
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
