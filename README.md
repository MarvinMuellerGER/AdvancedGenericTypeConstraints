# AdvancedGenericTypeConstraints

AdvancedGenericTypeConstraints enables compile-time-like validation for generic type rules in C# by combining lightweight
attributes with a Roslyn analyzer.

## Why this exists

Native C# generic constraints cannot express rules like:

- the supplied type must implement `IHandleMessages<T>` for some `T`
- the supplied type must carry a specific attribute
- one generic type argument must come from an assembly whose name is derived from another type argument's assembly

This project closes that gap with:

- a small abstractions package that exposes declarative attributes
- a Roslyn analyzer package that validates supplied type arguments at compile time

## Packages

Install both packages in the consuming project:

```xml
<ItemGroup>
  <PackageReference Include="AdvancedGenericTypeConstraints.Abstractions" Version="0.2.1" />
  <PackageReference Include="AdvancedGenericTypeConstraints.Analyzers" Version="0.2.1" PrivateAssets="all" />
</ItemGroup>
```

## Example

```csharp
using AdvancedGenericTypeConstraints;

[AttributeUsage(AttributeTargets.Class)]
public sealed class ServiceAttribute : Attribute;

public interface IHandleMessages<TMessage>;

public interface IFeatureRegistry
{
    void RegisterMessageHandler<
        [MustImplementOpenGeneric(typeof(IHandleMessages<>))]
        [MustHaveAttribute(typeof(ServiceAttribute))]
        TMessageHandler>();

    void RegisterServiceContract<
        [MustMatchAssemblyNameOf(nameof(TImplementation), suffix: ".Contracts")]
        TService,
        TImplementation>();
}
```

## Available checks

The current API supports:

- `MustImplementOpenGenericAttribute`
- `MustImplementOpenGenericAttribute(Type openGenericType, bool exactlyOne)`
- `MustNotImplementOpenGenericAttribute`
- `MustHaveAttributeAttribute`
- `MustMatchAssemblyNameOfAttribute`

## Diagnostic IDs

The analyzer currently emits these diagnostics:

- `AGTC001`: required open generic type is missing
- `AGTC002`: forbidden open generic type is present
- `AGTC003`: required open generic type is not matched exactly once
- `AGTC004`: invalid `MustImplementOpenGeneric` configuration on a generic parameter
- `AGTC005`: required attribute is missing
- `AGTC006`: assembly naming rule between two type arguments is violated
- `AGTC007`: `MustMatchAssemblyNameOf` references an invalid generic parameter

## Matching semantics

The analyzer validates both concrete type arguments and forwarded generic type parameters.

### Open generic checks

The analyzer compares open generic type definitions, not closed constructed types.

A type counts as a match when the configured open generic type definition appears on:

- the concrete type argument itself
- any base type in its inheritance chain
- any implemented interface

If a generic method or type forwards one of its own type parameters into another constrained generic API, the
forwarded type parameter also counts as a match when it already declares an equivalent or stricter
`MustImplementOpenGenericAttribute` constraint.

### Attribute checks

`MustHaveAttributeAttribute` checks whether the supplied type argument is directly annotated with the configured
attribute type. Derived attributes also satisfy the rule.

Forwarded generic type parameters are also accepted when they already declare the same
`MustHaveAttributeAttribute` constraint.

### Assembly naming checks

`MustMatchAssemblyNameOfAttribute` compares simple assembly names.

For a declaration like:

```csharp
void RegisterServiceContract<
    [MustMatchAssemblyNameOf(nameof(TImplementation), suffix: ".Contracts")]
    TService,
    TImplementation>();
```

the analyzer requires `TService` to come from an assembly named:

```text
{AssemblyOf(TImplementation)} + ".Contracts"
```

You can also configure:

- `prefix`
- `suffix`
- `AllowedTypes` as an explicit whitelist for legacy exceptions

Forwarded generic type parameters are also accepted when they already declare an equivalent or stricter
`MustMatchAssemblyNameOfAttribute` constraint. This allows delegating overloads and explicit interface
implementations to pass constrained generic parameters through without needing `#pragma warning disable AGTC006`.

Example:

```csharp
void RegisterServiceContract<
    [MustMatchAssemblyNameOf(
        nameof(TImplementation),
        suffix: ".Contracts",
        AllowedTypes = new Type[] { typeof(ICelestialPostService), typeof(IOrbitalEchoStore) })]
    TService,
    TImplementation>();
```

Forwarding example:

```csharp
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
```

## Non-goals

- no changes to the C# type system
- no runtime validation
- no replacement for native generic constraints

The solution is intentionally based on static analysis only.
