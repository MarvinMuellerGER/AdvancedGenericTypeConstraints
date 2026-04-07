// ReSharper disable CheckNamespace
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedTypeParameter
#pragma warning disable CA1050

using AdvancedGenericTypeConstraints;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface)]
public sealed class MarkerAttribute : Attribute;

public interface IHandleMessages<TMessage>;

public class MessageHandler<TMessage>;

public class AuditHandler<TMessage>;

public sealed class NoContractsImplementation;

public sealed class ForbiddenHandler : IHandleMessages<int>;

public sealed class DuplicateHandler : IHandleMessages<int>, IHandleMessages<string>;

public sealed class PlainHandler;

[Marker]
public sealed class DecoratedHandler;

public sealed class ServiceImplementation;

public interface IDemoRegistry
{
    void RequireOpenGeneric<[MustImplementOpenGeneric(typeof(IHandleMessages<>))] T>();

    void ForbidOpenGeneric<[MustNotImplementOpenGeneric(typeof(IHandleMessages<>))] T>();

    void RequireExactlyOne<[MustImplementOpenGeneric(typeof(IHandleMessages<>), true)] T>();

    void RequireAttribute<[MustHaveAttribute(typeof(MarkerAttribute))] T>();

    void RequireAssemblyMatch<
        [MustMatchAssemblyNameOf(nameof(TImplementation), suffix: ".Contracts")]
        TService,
        TImplementation>();

    void InvalidMustImplementConfig<
        [MustImplementOpenGeneric(typeof(MessageHandler<>))]
        [MustImplementOpenGeneric(typeof(AuditHandler<>))]
        T>();

    void InvalidAssemblyConfig<
        [MustMatchAssemblyNameOf("TMissing", suffix: ".Contracts")]
        TService,
        TImplementation>();
}

public static class Program
{
    public static void Main()
    {
        IDemoRegistry registry = null!;

        registry.RequireOpenGeneric<NoContractsImplementation>();
        registry.ForbidOpenGeneric<ForbiddenHandler>();
        registry.RequireExactlyOne<DuplicateHandler>();
        registry.RequireAttribute<PlainHandler>();
        registry.RequireAssemblyMatch<DecoratedHandler, ServiceImplementation>();
    }
}
