## Release 1.0.0

### New Rules

Rule ID | Category | Severity | Notes
OGC001 | Usage | Error | Type arguments for annotated generic parameters must implement the configured open generic
interface.
OGC002 | Usage | Error | Type arguments for annotated generic parameters must not match the configured open generic type
definition.
OGC003 | Usage | Error | Type arguments for annotated generic parameters must match the configured open generic type
definition exactly once.
OGC004 | Usage | Error | A generic parameter may declare at most one non-interface MustImplementOpenGeneric
constraint.
