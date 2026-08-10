; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
PHCTX001 | Pondhawk.Context | Error | Ambient context interfaces may contain properties only
PHCTX002 | Pondhawk.Context | Error | Ambient context type must be an interface
PHCTX003 | Pondhawk.Context | Error | Indexers are not supported
PHCTX004 | Pondhawk.Context | Error | Events are not supported
PHCTX005 | Pondhawk.Context | Error | Generic ambient context interfaces are not supported
PHCTX006 | Pondhawk.Context | Error | Duplicate or ambiguous inherited properties were detected
PHCTX007 | Pondhawk.Context | Error | Ambient context interfaces must be declared partial
PHCTX008 | Pondhawk.Context | Error | Init-only setters are not supported
PHCTX009 | Pondhawk.Context | Warning | Ambient context reference properties should be nullable
PHCTX010 | Pondhawk.Context | Error | Ambient contexts require C# 11 or later
PHCTX011 | Pondhawk.Context | Error | Nested ambient context interfaces are not supported
