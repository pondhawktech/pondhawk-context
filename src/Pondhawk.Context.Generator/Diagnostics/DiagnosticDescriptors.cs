using Microsoft.CodeAnalysis;

namespace Pondhawk.Context.Generator.Diagnostics;

/// <summary>
/// Compile-time diagnostics reported by the ambient context generator.
/// </summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "Pondhawk.Context";

    internal static readonly DiagnosticDescriptor PropertiesOnly = Error(
        "PHCTX001",
        "Ambient context interfaces may contain properties only",
        "Ambient context interface '{0}' declares member '{1}'. Ambient context interfaces may declare properties only.");

    internal static readonly DiagnosticDescriptor MustBeInterface = Error(
        "PHCTX002",
        "Ambient context type must be an interface",
        "'{0}' is marked with [AmbientContext] but is not an interface");

    internal static readonly DiagnosticDescriptor IndexersNotSupported = Error(
        "PHCTX003",
        "Indexers are not supported",
        "Ambient context interface '{0}' declares an indexer. Indexers cannot be mapped to a provider key.");

    internal static readonly DiagnosticDescriptor EventsNotSupported = Error(
        "PHCTX004",
        "Events are not supported",
        "Ambient context interface '{0}' declares event '{1}'. Ambient context values are not observable.");

    internal static readonly DiagnosticDescriptor GenericNotSupported = Error(
        "PHCTX005",
        "Generic ambient context interfaces are not supported",
        "Ambient context interface '{0}' is generic. Generic ambient context interfaces are not supported in version 1.");

    internal static readonly DiagnosticDescriptor AmbiguousProperty = Error(
        "PHCTX006",
        "Duplicate or ambiguous inherited properties were detected",
        "Ambient context interface '{0}' inherits conflicting declarations of property '{1}'. "
        + "Inherited properties with the same name must have the same type and accessors.");

    internal static readonly DiagnosticDescriptor MustBePartial = Error(
        "PHCTX007",
        "Ambient context interfaces must be declared partial",
        "Ambient context interface '{0}' must be declared 'partial' so the generator can implement it");

    internal static readonly DiagnosticDescriptor InitOnlyNotSupported = Error(
        "PHCTX008",
        "Init-only setters are not supported",
        "Property '{1}' on ambient context interface '{0}' declares an init-only setter. "
        + "Ambient context implementations are shared singletons and cannot be initialized per instance.");

    internal static readonly DiagnosticDescriptor NonNullableReferenceProperty = Warning(
        "PHCTX009",
        "Ambient context reference properties should be nullable",
        "Property '{1}' on ambient context interface '{0}' is a non-nullable reference type. "
        + "A missing ambient value reads as null, so the property should be declared nullable.");

    internal static readonly DiagnosticDescriptor LanguageVersionTooLow = Error(
        "PHCTX010",
        "Ambient contexts require C# 11 or later",
        "Ambient context interface '{0}' requires C# 11 or later. Set <LangVersion> to 11 or higher.");

    internal static readonly DiagnosticDescriptor NestedNotSupported = Error(
        "PHCTX011",
        "Nested ambient context interfaces are not supported",
        "Ambient context interface '{0}' is nested inside another type. Declare it at namespace scope.");

    private static DiagnosticDescriptor Error(string id, string title, string messageFormat) =>
        new(id, title, messageFormat, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);

    private static DiagnosticDescriptor Warning(string id, string title, string messageFormat) =>
        new(id, title, messageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true);
}
