# OpenGenericConstraints

OpenGenericConstraints enables compile-time-like validation for open generic constraints in C# by combining lightweight attributes with a Roslyn analyzer.

## Why this exists

C# generic constraints cannot express rules like "the supplied type must implement `IHandleMessages<T>` for some `T`" when the constraint targets an open generic type definition.

This project closes that gap with:

- a small abstractions package that exposes constraint attributes
- a Roslyn analyzer package that validates the supplied type arguments at compile time

## Packages

Install both packages in the consuming project:

```xml
<ItemGroup>
  <PackageReference Include="OpenGenericConstraints.Abstractions" Version="0.1.0" />
  <PackageReference Include="OpenGenericConstraints.Analyzers" Version="0.1.0" PrivateAssets="all" />
</ItemGroup>
```

## Example

```csharp
using OpenGenericConstraints;

public interface IHandleMessages<TMessage>
{
}

public interface IFeatureRegistry
{
    void RegisterMessageHandler<
        [MustImplementOpenGeneric(typeof(IHandleMessages<>))]
        TMessageHandler>();
}

public sealed class MyMessage
{
}

public sealed class MyHandler : IHandleMessages<MyMessage>
{
}
```

Using `RegisterMessageHandler<MyHandler>()` is valid.

Using a type that does not match the configured open generic definition produces:

```text
OGC001: Type 'MyHandler' must implement 'IHandleMessages<>'
```

## Matching semantics

The analyzer compares open generic type definitions, not closed constructed types.

A type counts as a match when the configured open generic type definition appears on:

- the concrete type argument itself
- any base type in its inheritance chain
- any implemented interface

This means matching is not limited to interfaces. Open generic classes, interfaces, and other generic type definitions are all supported as long as the open generic definition matches.

## Current API

Today the project supports `MustImplementOpenGenericAttribute`.

The next planned additions are:

- `MustNotImplementOpenGenericAttribute`
- an `exactlyOne` option on `MustImplementOpenGenericAttribute` to distinguish "at least one match" from "exactly one match"

## Non-goals

- no changes to the C# type system
- no runtime validation
- no replacement for native generic constraints

The solution is intentionally based on static analysis only.
