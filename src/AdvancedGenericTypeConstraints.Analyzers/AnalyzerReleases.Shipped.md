## Release 0.2.0

### New Rules

Rule ID | Category | Severity | Notes
AGTC001 | Usage | Error | Type arguments for annotated generic parameters must implement the configured open generic interface.
AGTC002 | Usage | Error | Type arguments for annotated generic parameters must not match the configured open generic type definition.
AGTC003 | Usage | Error | Type arguments for annotated generic parameters must match the configured open generic type definition exactly once.
AGTC004 | Usage | Error | A generic parameter may declare at most one non-interface MustImplementOpenGeneric constraint.
AGTC005 | Usage | Error | Type arguments for annotated generic parameters must be annotated with the configured attribute.
AGTC006 | Usage | Error | Type arguments for annotated generic parameters must belong to an assembly whose name matches another type argument's assembly according to the configured prefix and suffix.
AGTC007 | Usage | Error | A MustMatchAssemblyNameOf constraint must reference another generic parameter on the same type or method.

## Release 0.2.1

### New Rules

Rule ID | Category | Severity | Notes
AGTC008 | Usage | Error | Type values passed to parameters annotated with MustBeOpenGenericType must be open generic type definitions.

## Release 0.3.0

### New Rules

AGTC009 | Usage | Error | Type values passed to parameters annotated with MustBeReferenceType must represent reference types.
AGTC010 | Usage | Error | Type values passed to parameters annotated with MustBeAssignableTo must be assignable to the related type parameter.
AGTC011 | Usage | Error | A MustBeAssignableTo constraint must reference another System.Type method parameter on the same method.

## Release 0.4.0

### New Rules

Rule ID | Category | Severity | Notes
AGTC012 | Usage | Error | Type arguments for annotated generic parameters must match the configured type-name prefix and/or suffix.
AGTC013 | Usage | Error | A MustMatchTypeName constraint must define at least one non-empty value (prefix or suffix).
