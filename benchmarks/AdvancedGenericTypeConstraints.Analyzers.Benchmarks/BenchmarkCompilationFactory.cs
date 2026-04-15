using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace AdvancedGenericTypeConstraints.Analyzers.Benchmarks;

internal static class BenchmarkCompilationFactory
{
    public static Compilation CreateCompilation(int scenarioCount)
    {
        var syntaxTrees = CreateSyntaxTrees(scenarioCount);
        var references = CreateMetadataReferences();

        return CSharpCompilation.Create(
            assemblyName: "AdvancedGenericTypeConstraints.Benchmarks.Generated",
            syntaxTrees: syntaxTrees,
            references: references,
            options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static ImmutableArray<SyntaxTree> CreateSyntaxTrees(int scenarioCount)
    {
        var builder = ImmutableArray.CreateBuilder<SyntaxTree>(scenarioCount + 1);
        builder.Add(CSharpSyntaxTree.ParseText(
            """
            using System;
            using AdvancedGenericTypeConstraints;

            [AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
            public sealed class ServiceAttribute : Attribute
            {
            }

            public interface IHandleMessages<TMessage>
            {
            }

            public interface IMarkerService
            {
            }
            """));

        for (var index = 0; index < scenarioCount; index++)
        {
            builder.Add(CSharpSyntaxTree.ParseText(BuildScenarioSource(index)));
        }

        return builder.ToImmutable();
    }

    private static string BuildScenarioSource(int index)
        => $$"""
           using System;
           using AdvancedGenericTypeConstraints;

           namespace BenchmarkScenario{{index}};

           public sealed class Message{{index}}
           {
           }

           public interface IService{{index}} : IMarkerService
           {
           }

           [Service]
           public sealed class Good{{index}}Handler : IHandleMessages<Message{{index}}>, IService{{index}}
           {
           }

           public sealed class Bad{{index}}Handler : IHandleMessages<Message{{index}}>
           {
           }

           public sealed class Plain{{index}}
           {
           }

           public interface IRegistry{{index}}
           {
               void Register<[MustImplementOpenGeneric(typeof(IHandleMessages<>), true)] [MustHaveAttribute(typeof(ServiceAttribute))] [MustMatchTypeName(prefix: "Good", suffix: "Handler")] THandler>();
               void Wire([MustBeOpenGenericType] Type openGenericType, [MustBeReferenceType] Type serviceType, [MustBeAssignableTo(nameof(serviceType))] Type implementationType);
           }

           public sealed class Registry{{index}} : IRegistry{{index}}
           {
               public void Register<[MustImplementOpenGeneric(typeof(IHandleMessages<>), true)] [MustHaveAttribute(typeof(ServiceAttribute))] [MustMatchTypeName(prefix: "Good", suffix: "Handler")] THandler>()
               {
               }

               public void Wire([MustBeOpenGenericType] Type openGenericType, [MustBeReferenceType] Type serviceType, [MustBeAssignableTo(nameof(serviceType))] Type implementationType)
               {
               }
           }

           public sealed class Feature{{index}}<[MustImplementOpenGeneric(typeof(IHandleMessages<>))] THandler>
           {
           }

           public static class Demo{{index}}
           {
               public static void Run(IRegistry{{index}} registry)
               {
                   registry.Register<Good{{index}}Handler>();
                   registry.Register<Bad{{index}}Handler>();
                   registry.Register<Plain{{index}}>();
                   registry.Wire(typeof(IHandleMessages<>), typeof(IService{{index}}), typeof(Good{{index}}Handler));
                   registry.Wire(typeof(IHandleMessages<>), typeof(IService{{index}}), typeof(Bad{{index}}Handler));
                   registry.Wire(typeof(Message{{index}}), typeof(IService{{index}}), typeof(Plain{{index}}));
                   _ = new Feature<Good{{index}}Handler>();
                   _ = new Feature<Plain{{index}}>();
               }
           }
           """;

    private static ImmutableArray<MetadataReference> CreateMetadataReferences()
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = ImmutableArray.CreateBuilder<MetadataReference>();
        var trustedPlatformAssemblies = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            ?? [];

        foreach (var path in trustedPlatformAssemblies)
        {
            if (paths.Add(path))
                references.Add(MetadataReference.CreateFromFile(path));
        }

        foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
        {
            if (assembly.IsDynamic || string.IsNullOrEmpty(assembly.Location))
                continue;

            if (paths.Add(assembly.Location))
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }

        AddReference(typeof(MustImplementOpenGenericAttribute).Assembly);
        AddReference(typeof(Enumerable).Assembly);
        AddReference(typeof(CSharpCompilation).Assembly);

        return references.ToImmutable();

        void AddReference(Assembly assembly)
        {
            if (!string.IsNullOrEmpty(assembly.Location) && paths.Add(assembly.Location))
                references.Add(MetadataReference.CreateFromFile(assembly.Location));
        }
    }
}
