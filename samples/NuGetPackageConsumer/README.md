# NuGet Package Consumer

This sample shows how a consuming project uses the constraints and analyzer:

- `AdvancedGenericTypeConstraints.Abstractions` `0.4.1`
- `AdvancedGenericTypeConstraints.Analyzers` `0.4.1`

By default, `NuGetPackageConsumer.csproj` uses local `ProjectReference`s (`UseLocalProjects=true`) so the sample is
always aligned with the current repository state. Set `UseLocalProjects=false` to consume published NuGet packages
instead.

The sample intentionally triggers each analyzer rule exactly once during build:

- `AGTC001` missing required open generic
- `AGTC002` forbidden open generic present
- `AGTC003` required open generic matched more than once
- `AGTC004` invalid `MustImplementOpenGeneric` configuration
- `AGTC005` missing required attribute
- `AGTC006` assembly naming rule violation
- `AGTC007` invalid `MustMatchAssemblyNameOf` related-parameter reference
- `AGTC008` `Type` argument is not an open generic type definition
- `AGTC009` `Type` argument is not a reference type
- `AGTC010` `Type` argument is not assignable to the related `Type` argument
- `AGTC011` invalid `MustBeAssignableTo` related-parameter reference
- `AGTC012` generic type argument name does not match configured prefix/suffix
- `AGTC013` invalid `MustMatchTypeName` configuration (missing prefix and suffix)
