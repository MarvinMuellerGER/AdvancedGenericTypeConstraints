# NuGet Package Consumer

This sample shows how a consuming project references the published NuGet packages:

- `AdvancedGenericTypeConstraints.Abstractions` `0.2.1`
- `AdvancedGenericTypeConstraints.Analyzers` `0.2.1`

The sample intentionally triggers each analyzer rule exactly once during build:

- `AGTC001` missing required open generic
- `AGTC002` forbidden open generic present
- `AGTC003` required open generic matched more than once
- `AGTC004` invalid `MustImplementOpenGeneric` configuration
- `AGTC005` missing required attribute
- `AGTC006` assembly naming rule violation
- `AGTC007` invalid `MustMatchAssemblyNameOf` related-parameter reference
- `AGTC008` `Type` argument is not an open generic type definition
