using System;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Pondhawk.Context.Generator.Models;

/// <summary>
/// A diagnostic captured by value, so it can travel through the incremental pipeline
/// and be reported during source output.
/// </summary>
internal sealed class DiagnosticInfo : IEquatable<DiagnosticInfo>
{
    public DiagnosticInfo(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] messageArgs)
    {
        Descriptor = descriptor;
        Location = location;
        MessageArgs = messageArgs;
    }

    public DiagnosticDescriptor Descriptor { get; }

    public LocationInfo? Location { get; }

    public string[] MessageArgs { get; }

    public Diagnostic ToDiagnostic() =>
        Diagnostic.Create(Descriptor, Location?.ToLocation(), MessageArgs);

    public bool Equals(DiagnosticInfo? other) =>
        other is not null
        && StringComparer.Ordinal.Equals(Descriptor.Id, other.Descriptor.Id)
        && Equals(Location, other.Location)
        && MessageArgs.SequenceEqual(other.MessageArgs, StringComparer.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as DiagnosticInfo);

    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Descriptor.Id);
}
