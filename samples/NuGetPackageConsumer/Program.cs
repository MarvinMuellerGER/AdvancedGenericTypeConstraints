using AdvancedGenericTypeConstraints;

[AttributeUsage(AttributeTargets.Class)]
public sealed class HandlerAttribute : Attribute
{
}

public interface IHandleMessages<TMessage>;

public interface IFeatureRegistry
{
    void Register<
        [MustImplementOpenGeneric(typeof(IHandleMessages<>))]
        [MustHaveAttribute(typeof(HandlerAttribute))]
        THandler>();
}

[Handler]
public sealed class MyHandler : IHandleMessages<string>;
