using System.Collections.Immutable;
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
    public async Task ReportsNoDiagnostic_When_TypeNameMatchesConfiguredPrefixAndSuffix()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

                              public interface IRegistry
                              {
                                  void Register<[MustMatchTypeName(prefix: "I", suffix: "Service")] TService>();
                              }

                              public interface IArcaneService
                              {
                              }

                              public static class Demo
                              {
                                  public static void Run(IRegistry registry)
                                  {
                                      registry.Register<IArcaneService>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsDiagnostic_When_TypeNameDoesNotMatchConfiguredPrefixAndSuffix()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

                              public interface IRegistry
                              {
                                  void Register<[MustMatchTypeName(prefix: "I", suffix: "Service")] TService>();
                              }

                              public interface ArcaneHandler
                              {
                              }

                              public static class Demo
                              {
                                  public static void Run(IRegistry registry)
                                  {
                                      registry.Register<ArcaneHandler>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustMatchTypeNameDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'ArcaneHandler' name must start with 'I' and end with 'Service'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_TypeNameConstraintIsForwardedThroughGenericMethod()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

                              public interface IRegistry
                              {
                                  void Register<[MustMatchTypeName(prefix: "I")] TService>();
                              }

                              public sealed class Registry : IRegistry
                              {
                                  public void Register<[MustMatchTypeName(prefix: "I")] TService>()
                                  {
                                  }

                                  void IRegistry.Register<[MustMatchTypeName(prefix: "I")] TService>()
                                  {
                                      Register<TService>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsDiagnostic_When_TypeNameConstraintIsConfiguredWithoutPrefixAndSuffix()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

                              public interface IRegistry
                              {
                                  void Register<[MustMatchTypeName] TService>();
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.InvalidTypeNameConstraintConfigurationDiagnosticId, diagnostic.Id);
        Assert.Equal(
            "Generic parameter 'TService' declares MustMatchTypeName without a prefix or suffix",
            diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_MustImplementConstraintIsForwardedThroughGenericMethod()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

                              public interface IHandler<T>
                              {
                              }

                              public interface IRegistry
                              {
                                  void Register<[MustImplementOpenGeneric(typeof(IHandler<>))] TService>();
                              }

                              public sealed class Registry : IRegistry
                              {
                                  public void Register<[MustImplementOpenGeneric(typeof(IHandler<>))] TService>()
                                  {
                                  }

                                  void IRegistry.Register<[MustImplementOpenGeneric(typeof(IHandler<>))] TService>()
                                  {
                                      Register<TService>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_MustHaveAttributeConstraintIsForwardedThroughGenericMethod()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
                              public sealed class ServiceAttribute : Attribute
                              {
                              }

                              public interface IRegistry
                              {
                                  void Register<[MustHaveAttribute(typeof(ServiceAttribute))] TService>();
                              }

                              public sealed class Registry : IRegistry
                              {
                                  public void Register<[MustHaveAttribute(typeof(ServiceAttribute))] TService>()
                                  {
                                  }

                                  void IRegistry.Register<[MustHaveAttribute(typeof(ServiceAttribute))] TService>()
                                  {
                                      Register<TService>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
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
    public async Task ReportsNoDiagnostic_When_AssemblyConstraintIsForwardedThroughGenericMethod()
    {
        const string source = """
                              using AdvancedGenericTypeConstraints;

                              public interface IFeatureRegistry
                              {
                                  IFeatureRegistry RegisterInProcessApi<
                                      [MustMatchAssemblyNameOf(nameof(TImplementation), suffix: ".Contracts")] TService,
                                      TImplementation>()
                                      where TService : class
                                      where TImplementation : class, TService;
                              }

                              public sealed class ConfiguredFeatureRegistry : IFeatureRegistry
                              {
                                  public ConfiguredFeatureRegistry RegisterInProcessApi<
                                      [MustMatchAssemblyNameOf(nameof(TImplementation), suffix: ".Contracts")] TService,
                                      TImplementation>()
                                      where TService : class
                                      where TImplementation : class, TService
                                  {
                                      return this;
                                  }

                                  IFeatureRegistry IFeatureRegistry.RegisterInProcessApi<
                                      [MustMatchAssemblyNameOf(nameof(TImplementation), suffix: ".Contracts")] TService,
                                      TImplementation>()
                                  {
                                      return RegisterInProcessApi<TService, TImplementation>();
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_TypeParameterAssemblyNamesMatch()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IRegistry
                              {
                                  void RegisterInProcessApi(
                                      [MustMatchAssemblyNameOf(nameof(implementationType), suffix: ".Contracts")] Type serviceType,
                                      Type implementationType);
                              }

                              public static class Demo
                              {
                                  public static void Run(IRegistry registry)
                                  {
                                      registry.RegisterInProcessApi(typeof(Feature.Contracts.IService), typeof(Feature.ServiceImplementation));
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
    public async Task ReportsDiagnostic_When_TypeParameterAssemblyNamesDoNotMatch()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IRegistry
                              {
                                  void RegisterInProcessApi(
                                      [MustMatchAssemblyNameOf(nameof(implementationType), suffix: ".Contracts")] Type serviceType,
                                      Type implementationType);
                              }

                              public static class Demo
                              {
                                  public static void Run(IRegistry registry)
                                  {
                                      registry.RegisterInProcessApi(typeof(Legacy.Contracts.IService), typeof(Feature.ServiceImplementation));
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
    public async Task ReportsNoDiagnostic_When_TypeParameterIsAnOpenGenericTypeDefinition()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface ICommandService<T>
                              {
                              }

                              public interface IFeatureRegistry
                              {
                                  void Register([MustBeOpenGenericType] Type serviceType);
                              }

                              public static class Demo
                              {
                                  public static void Run(IFeatureRegistry featureRegistry)
                                  {
                                      featureRegistry.Register(typeof(ICommandService<>));
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsDiagnostic_When_TypeParameterIsAClosedGenericType()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface ICommandService<T>
                              {
                              }

                              public sealed class Command
                              {
                              }

                              public interface IFeatureRegistry
                              {
                                  void Register([MustBeOpenGenericType] Type serviceType);
                              }

                              public static class Demo
                              {
                                  public static void Run(IFeatureRegistry featureRegistry)
                                  {
                                      featureRegistry.Register(typeof(ICommandService<Command>));
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustBeOpenGenericTypeDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'ICommandService<Command>' must be an open generic type definition", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsDiagnostic_When_TypeParameterIsNotGeneric()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IFeatureRegistry
                              {
                                  void Register([MustBeOpenGenericType] Type serviceType);
                              }

                              public static class Demo
                              {
                                  public static void Run(IFeatureRegistry featureRegistry)
                                  {
                                      featureRegistry.Register(typeof(string));
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustBeOpenGenericTypeDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'string' must be an open generic type definition", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_TypeParameterIsAReferenceType()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IFeatureRegistry
                              {
                                  void Register([MustBeReferenceType] Type serviceType);
                              }

                              public static class Demo
                              {
                                  public static void Run(IFeatureRegistry featureRegistry)
                                  {
                                      featureRegistry.Register(typeof(string));
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsDiagnostic_When_TypeParameterIsNotAReferenceType()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IFeatureRegistry
                              {
                                  void Register([MustBeReferenceType] Type serviceType);
                              }

                              public static class Demo
                              {
                                  public static void Run(IFeatureRegistry featureRegistry)
                                  {
                                      featureRegistry.Register(typeof(int));
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustBeReferenceTypeDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'int' must be a reference type", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_TypeParameterIsAssignableToRelatedType()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IService<T>
                              {
                              }

                              public sealed class Implementation<T> : IService<T>
                              {
                              }

                              public interface IFeatureRegistry
                              {
                                  void Register(
                                      Type serviceType,
                                      [MustBeAssignableTo(nameof(serviceType))] Type implementationType);
                              }

                              public static class Demo
                              {
                                  public static void Run(IFeatureRegistry featureRegistry)
                                  {
                                      featureRegistry.Register(typeof(IService<>), typeof(Implementation<>));
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsDiagnostic_When_TypeParameterIsNotAssignableToRelatedType()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IService<T>
                              {
                              }

                              public sealed class OtherService<T>
                              {
                              }

                              public interface IFeatureRegistry
                              {
                                  void Register(
                                      Type serviceType,
                                      [MustBeAssignableTo(nameof(serviceType))] Type implementationType);
                              }

                              public static class Demo
                              {
                                  public static void Run(IFeatureRegistry featureRegistry)
                                  {
                                      featureRegistry.Register(typeof(IService<>), typeof(OtherService<>));
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustBeAssignableToDiagnosticId, diagnostic.Id);
        Assert.Equal("Type 'OtherService<>' must be assignable to type 'IService<>'", diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_OpenGenericTypeConstraintIsForwardedThroughTypeParameter()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IFeatureRegistry
                              {
                                  void Register([MustBeOpenGenericType] Type serviceType);
                              }

                              public sealed class FeatureRegistry : IFeatureRegistry
                              {
                                  public void Register([MustBeOpenGenericType] Type serviceType)
                                  {
                                  }

                                  public void Forward([MustBeOpenGenericType] Type serviceType)
                                  {
                                      Register(serviceType);
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_ReferenceTypeConstraintIsForwardedThroughTypeParameter()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public sealed class FeatureRegistry
                              {
                                  public void Register([MustBeReferenceType] Type serviceType)
                                  {
                                  }

                                  public void Forward([MustBeReferenceType] Type serviceType)
                                  {
                                      Register(serviceType);
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_AssignableToConstraintIsForwardedThroughTypeParameters()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public sealed class FeatureRegistry
                              {
                                  public void Register(
                                      Type serviceType,
                                      [MustBeAssignableTo(nameof(serviceType))] Type implementationType)
                                  {
                                  }

                                  public void Forward(
                                      Type serviceType,
                                      [MustBeAssignableTo(nameof(serviceType))] Type implementationType)
                                  {
                                      Register(serviceType, implementationType);
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_TypeParameterAssemblyConstraintIsSatisfiedViaGenericForwarding()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IFeatureRegistry
                              {
                                  IFeatureRegistry RegisterInProcessApi<
                                      [MustMatchAssemblyNameOf(nameof(TImplementation), suffix: ".Contracts")] TService,
                                      TImplementation>()
                                      where TService : class
                                      where TImplementation : class, TService;

                                  IFeatureRegistry RegisterInProcessApi(
                                      [MustMatchAssemblyNameOf(nameof(implementationType), suffix: ".Contracts")] Type serviceType,
                                      Type implementationType);
                              }

                              public sealed class ConfiguredFeatureRegistry : IFeatureRegistry
                              {
                                  public IFeatureRegistry RegisterInProcessApi<
                                      [MustMatchAssemblyNameOf(nameof(TImplementation), suffix: ".Contracts")] TService,
                                      TImplementation>()
                                      where TService : class
                                      where TImplementation : class, TService
                                  {
                                      return RegisterInProcessApi(typeof(TService), typeof(TImplementation));
                                  }

                                  public IFeatureRegistry RegisterInProcessApi(
                                      [MustMatchAssemblyNameOf(nameof(implementationType), suffix: ".Contracts")] Type serviceType,
                                      Type implementationType)
                                  {
                                      return this;
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsNoDiagnostic_When_TypeParameterAssemblyConstraintIsForwardedThroughTypeParameters()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public sealed class FeatureRegistry
                              {
                                  public void RegisterInProcessApi(
                                      [MustMatchAssemblyNameOf(nameof(implementationType), suffix: ".Contracts")] Type serviceType,
                                      Type implementationType)
                                  {
                                  }

                                  public void Forward(
                                      [MustMatchAssemblyNameOf(nameof(implementationType), suffix: ".Contracts")] Type serviceType,
                                      Type implementationType)
                                  {
                                      RegisterInProcessApi(serviceType, implementationType);
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public async Task ReportsDiagnostic_When_TypeParameterAssemblyConstraintIsForwardedWithoutEquivalentConstraint()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IFeatureRegistry
                              {
                                  IFeatureRegistry RegisterInProcessApi(
                                      [MustBeOpenGenericType] Type serviceType,
                                      [MustBeOpenGenericType] Type implementationType);
                              }

                              public sealed class ConfiguredFeatureRegistry : IFeatureRegistry
                              {
                                  IFeatureRegistry IFeatureRegistry.RegisterInProcessApi(
                                      [MustBeOpenGenericType] Type serviceType,
                                      [MustBeOpenGenericType] Type implementationType)
                                  {
                                      return RegisterInProcessApi(serviceType, implementationType);
                                  }

                                  public ConfiguredFeatureRegistry RegisterInProcessApi(
                                      [MustBeOpenGenericType]
                                      [MustMatchAssemblyNameOf(nameof(implementationType), suffix: ".Contracts")] Type serviceType,
                                      [MustBeOpenGenericType] Type implementationType)
                                  {
                                      return this;
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustMatchAssemblyNameDiagnosticId, diagnostic.Id);
        Assert.Equal(
            "Type 'serviceType' must be declared in assembly '{AssemblyOf(implementationType)}.Contracts' to match type 'implementationType'",
            diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsDiagnostic_When_OnlyRelatedTypeParameterIsForwardedWithoutEquivalentConstraint()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public sealed class FeatureRegistry
                              {
                                  public void RegisterInProcessApi(
                                      [MustMatchAssemblyNameOf(nameof(implementationType), suffix: ".Contracts")] Type serviceType,
                                      Type implementationType)
                                  {
                                  }

                                  public void Forward(Type implementationType)
                                  {
                                      RegisterInProcessApi(typeof(Service.Contracts.IService<>), implementationType);
                                  }
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(
            source,
            CreateAssemblyReference(
                "Service.Contracts",
                "namespace Service.Contracts { public interface IService<T> { } }"));

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.MustMatchAssemblyNameDiagnosticId, diagnostic.Id);
        Assert.Equal(
            "Type 'IService<>' must be declared in assembly '{AssemblyOf(implementationType)}.Contracts' to match type 'implementationType'",
            diagnostic.GetMessage());
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
            "Parameter or generic parameter 'TService' references invalid related parameter 'TMissing'",
            diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsDiagnostic_When_AssemblyConstraintReferencesUnknownMethodParameter()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IRegistry
                              {
                                  void Register(
                                      [MustMatchAssemblyNameOf("missingImplementationType", suffix: ".Contracts")] Type serviceType,
                                      Type implementationType);
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.InvalidAssemblyConstraintConfigurationDiagnosticId, diagnostic.Id);
        Assert.Equal(
            "Parameter or generic parameter 'serviceType' references invalid related parameter 'missingImplementationType'",
            diagnostic.GetMessage());
    }

    [Fact]
    public async Task ReportsDiagnostic_When_AssignableToConstraintReferencesUnknownMethodParameter()
    {
        const string source = """
                              using System;
                              using AdvancedGenericTypeConstraints;

                              public interface IRegistry
                              {
                                  void Register(
                                      Type serviceType,
                                      [MustBeAssignableTo("missingServiceType")] Type implementationType);
                              }
                              """;

        var diagnostics = await GetDiagnosticsAsync(source);

        var diagnostic = Assert.Single(diagnostics);
        Assert.Equal(AdvancedGenericTypeConstraintAnalyzer.InvalidAssignableToConstraintConfigurationDiagnosticId, diagnostic.Id);
        Assert.Equal(
            "Parameter 'implementationType' references invalid related parameter 'missingServiceType'",
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
            .ToArray<MetadataReference>();
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
