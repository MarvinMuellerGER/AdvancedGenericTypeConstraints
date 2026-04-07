using System.Collections.Immutable;
using AdvancedGenericTypeConstraints;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AdvancedGenericTypeConstraints.Analyzers.Tests;

public class AdvancedGenericTypeConstraintAnalyzerTests
{
    [Fact]
    public async Task ReportsNoDiagnostic_When_TypeImplementsRequiredOpenGeneric()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

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
                              using AdvancedGenericTypeConstraints;

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
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustImplementDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must implement 'IHandleMessages<>'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsDiagnostic_When_GenericTypeUsageViolatesConstraint()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

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
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustImplementDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must implement 'IHandleMessages<>'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_GenericBaseTypeMatches()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

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
                              using AdvancedGenericTypeConstraints;

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
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustNotImplementDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must not implement 'IHandleMessages<>'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsDiagnostic_When_ExactlyOneMatchIsRequiredButMultipleExist()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

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
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustImplementExactlyOneDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyHandler' must implement 'IHandleMessages<>' exactly once", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsDiagnostic_When_MultipleMustImplementConstraintsTargetConcreteTypes()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

                              public class MessageHandler<TMessage> { }
                              public class AuditHandler<TMessage> { }

                              public interface IFeatureRegistry
                              {
                                  void Register<
                                      [MustImplementOpenGeneric(typeof(MessageHandler<>))]
                                      [MustImplementOpenGeneric(typeof(AuditHandler<>))]
                                      THandler>();
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.InvalidMustImplementConfigurationDiagnosticId, diagnostic.Id);
        Assert.Equal(
            "Generic parameter 'THandler' can declare at most one non-interface MustImplementOpenGeneric constraint",
            diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_OneConcreteTypeAndMultipleInterfacesAreRequired()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

                              public class MessageHandler<TMessage> { }
                              public interface IHandleMessages<TMessage> { }
                              public interface ILogMessages<TMessage> { }

                              public sealed class MyMessage { }

                              public sealed class MyHandler : MessageHandler<MyMessage>, IHandleMessages<MyMessage>, ILogMessages<MyMessage>
                              {
                              }

                              public interface IFeatureRegistry
                              {
                                  void Register<
                                      [MustImplementOpenGeneric(typeof(MessageHandler<>))]
                                      [MustImplementOpenGeneric(typeof(IHandleMessages<>))]
                                      [MustImplementOpenGeneric(typeof(ILogMessages<>))]
                                      THandler>();
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
    public async Task ReportsNoDiagnostic_When_TypeHasRequiredAttribute()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
                              public sealed class ServiceAttribute : Attribute
                              {
                              }

                              [Service]
                              public sealed class MyService
                              {
                              }

                              public interface IRegistry
                              {
                                  void Register<[MustHaveAttribute(typeof(ServiceAttribute))] TService>();
                              }

                              public static class Demo
                              {
                                  public static void Run(IRegistry registry)
                                  {
                                      registry.Register<MyService>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsDiagnostic_When_TypeDoesNotHaveRequiredAttribute()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
                              public sealed class ServiceAttribute : Attribute
                              {
                              }

                              public sealed class MyService
                              {
                              }

                              public interface IRegistry
                              {
                                  void Register<[MustHaveAttribute(typeof(ServiceAttribute))] TService>();
                              }

                              public static class Demo
                              {
                                  public static void Run(IRegistry registry)
                                  {
                                      registry.Register<MyService>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustHaveAttributeDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'MyService' must be annotated with 'ServiceAttribute'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_AssemblyNameMatchesConfiguredSuffix()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

                              public interface IRegistry
                              {
                                  void RegisterServiceContract<
                                      [MustMatchAssemblyNameOf(nameof(TImplementation), suffix: ".Contracts")] TService,
                                      TImplementation>();
                              }

                              public static class Demo
                              {
                                  public static void Run(IRegistry registry)
                                  {
                                      registry.RegisterServiceContract<Feature.Contracts.IService, Feature.ServiceImplementation>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(
            source,
            CreateAssemblyReference(
                "Feature.Contracts",
                "namespace Feature.Contracts { public interface IService { } }"),
            CreateAssemblyReference(
                "Feature",
                "namespace Feature { public sealed class ServiceImplementation { } }"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsDiagnostic_When_AssemblyNameDoesNotMatchConfiguredSuffix()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

                              public interface IRegistry
                              {
                                  void RegisterServiceContract<
                                      [MustMatchAssemblyNameOf(nameof(TImplementation), suffix: ".Contracts")] TService,
                                      TImplementation>();
                              }

                              public static class Demo
                              {
                                  public static void Run(IRegistry registry)
                                  {
                                      registry.RegisterServiceContract<Legacy.Contracts.IService, Feature.ServiceImplementation>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(
            source,
            CreateAssemblyReference(
                "Legacy.Contracts",
                "namespace Legacy.Contracts { public interface IService { } }"),
            CreateAssemblyReference(
                "Feature",
                "namespace Feature { public sealed class ServiceImplementation { } }"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustMatchAssemblyNameDiagnosticId, diagnostic.Id);
        Assert.Equal(
            "Type 'IService' must be declared in assembly 'Feature.Contracts' to match type 'ServiceImplementation'",
            diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_AssemblyRuleAllowsWhitelistedType()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IRegistry
                              {
                                  void RegisterServiceContract<
                                      [MustMatchAssemblyNameOf(nameof(TImplementation), suffix: ".Contracts", AllowedTypes = new Type[] { typeof(Legacy.Contracts.ICelestialPostService) })] TService,
                                      TImplementation>();
                              }

                              public static class Demo
                              {
                                  public static void Run(IRegistry registry)
                                  {
                                      registry.RegisterServiceContract<Legacy.Contracts.ICelestialPostService, Feature.ServiceImplementation>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(
            source,
            CreateAssemblyReference(
                "Legacy.Contracts",
                "namespace Legacy.Contracts { public interface ICelestialPostService { } }"),
            CreateAssemblyReference(
                "Feature",
                "namespace Feature { public sealed class ServiceImplementation { } }"));

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsDiagnostic_When_AssemblyConstraintReferencesUnknownTypeParameter()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

                              public interface IRegistry
                              {
                                  void Register<
                                      [MustMatchAssemblyNameOf("TMissing", suffix: ".Contracts")] TService,
                                      TImplementation>();
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.InvalidAssemblyConstraintConfigurationDiagnosticId, diagnostic.Id);
        Assert.Equal(
            "Generic parameter 'TService' references invalid related parameter 'TMissing'",
            diagnostic.GetMessage());
    }

    private static async Task<ImmutableArray<Diagnostic>> GetDiagnosticsAsync(
        string source,
        params MetadataReference[] additionalReferences)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));

        var references = GetMetadataReferences()
            .Concat(additionalReferences)
            .DistinctBy(static reference => reference.Display, StringComparer.OrdinalIgnoreCase)
            .ToArray();

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

        var analyzer = new AdvancedGenericTypeConstraintAnalyzer();
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
            .ToArray();
    }

    private static MetadataReference CreateAssemblyReference(string assemblyName, string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source, new CSharpParseOptions(LanguageVersion.Preview));
        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            GetMetadataReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        var emitResult = compilation.Emit(stream);

        Assert.True(
            emitResult.Success,
            "Reference compilation failed:\n" + string.Join(
                Environment.NewLine,
                emitResult.Diagnostics.Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Error)));

        stream.Position = 0;
        return MetadataReference.CreateFromImage(stream.ToArray(), filePath: $"{assemblyName}.dll");
    }
}
