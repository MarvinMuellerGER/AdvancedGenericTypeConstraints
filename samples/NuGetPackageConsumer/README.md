# NuGet package consumer smoke test

This sample intentionally fails to build so you can verify that the published NuGet packages are active in a real consumer project.

## What it does

The project references:

- `OpenGenericConstraints.Abstractions` `0.1.0`
- `OpenGenericConstraints.Analyzers` `0.1.0`

`Program.cs` then passes a type that does not implement `IHandleMessages<>`, so the analyzer should report `OGC001`.

## How to run it

From the repository root:

```bash
dotnet build samples/NuGetPackageConsumer/NuGetPackageConsumer.csproj
```

Expected result:

```text
error OGC001: Type 'MyHandler' must implement 'IHandleMessages<>'
```

## Notes

- The sample restores packages directly from NuGet.org.
- This project is intentionally not part of the solution because it is supposed to fail.
