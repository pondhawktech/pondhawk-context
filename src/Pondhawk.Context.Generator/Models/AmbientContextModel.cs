using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Pondhawk.Context.Generator.Models;

/// <summary>
/// Everything the emitter needs about one <c>[AmbientContext]</c> interface, plus any
/// diagnostics discovered while reading it.
/// </summary>
internal sealed class AmbientContextModel : IEquatable<AmbientContextModel>
{
    public AmbientContextModel(
        string? containingNamespace,
        string interfaceName,
        string qualifiedInterfaceName,
        string accessibility,
        string implementationName,
        string hintName,
        ImmutableArray<AmbientPropertyModel> properties,
        ImmutableArray<DiagnosticInfo> diagnostics)
    {
        ContainingNamespace = containingNamespace;
        InterfaceName = interfaceName;
        QualifiedInterfaceName = qualifiedInterfaceName;
        Accessibility = accessibility;
        ImplementationName = implementationName;
        HintName = hintName;
        Properties = properties;
        Diagnostics = diagnostics;
    }

    /// <summary>Null when the interface is declared in the global namespace.</summary>
    public string? ContainingNamespace { get; }

    public string InterfaceName { get; }

    /// <summary>Fully qualified, <c>global::</c>-prefixed interface name.</summary>
    public string QualifiedInterfaceName { get; }

    public string Accessibility { get; }

    public string ImplementationName { get; }

    public string HintName { get; }

    public ImmutableArray<AmbientPropertyModel> Properties { get; }

    public ImmutableArray<DiagnosticInfo> Diagnostics { get; }

    /// <summary>
    /// True when the model is sound enough to emit. Errors suppress emission so the
    /// consumer sees the diagnostic rather than a cascade of failures in generated
    /// code; warnings do not.
    /// </summary>
    public bool CanEmit =>
        !Diagnostics.Any(diagnostic => diagnostic.Descriptor.DefaultSeverity == DiagnosticSeverity.Error);

    public bool Equals(AmbientContextModel? other) =>
        other is not null
        && StringComparer.Ordinal.Equals(ContainingNamespace, other.ContainingNamespace)
        && StringComparer.Ordinal.Equals(QualifiedInterfaceName, other.QualifiedInterfaceName)
        && StringComparer.Ordinal.Equals(Accessibility, other.Accessibility)
        && StringComparer.Ordinal.Equals(ImplementationName, other.ImplementationName)
        && Properties.SequenceEqual(other.Properties)
        && Diagnostics.SequenceEqual(other.Diagnostics);

    public override bool Equals(object? obj) => Equals(obj as AmbientContextModel);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(QualifiedInterfaceName);
}
