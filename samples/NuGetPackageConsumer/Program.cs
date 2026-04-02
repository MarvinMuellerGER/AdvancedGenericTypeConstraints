using OpenGenericConstraints;

public interface IHandleMessages<TMessage>
{
}

public interface IFeatureRegistry
{
    void Register<[MustImplementOpenGeneric(typeof(IHandleMessages<>))] THandler>();
}

public sealed class MyHandler
{
}

public static class Demo
{
    public static void Run(IFeatureRegistry registry)
    {
        registry.Register<MyHandler>();
    }
}
