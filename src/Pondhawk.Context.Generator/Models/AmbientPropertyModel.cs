using System;

namespace Pondhawk.Context.Generator.Models;

/// <summary>
/// A single ambient property, reduced to exactly what emission needs.
/// </summary>
internal sealed class AmbientPropertyModel : IEquatable<AmbientPropertyModel>
{
    public AmbientPropertyModel(
        string name,
        string declaredType,
        string storageType,
        bool hasGetter,
        bool hasSetter,
        bool suppressNullWarning)
    {
        Name = name;
        DeclaredType = declaredType;
        StorageType = storageType;
        HasGetter = hasGetter;
        HasSetter = hasSetter;
        SuppressNullWarning = suppressNullWarning;
    }

    /// <summary>The property name, which is also the provider key.</summary>
    public string Name { get; }

    /// <summary>The property type as declared, including nullable annotations.</summary>
    public string DeclaredType { get; }

    /// <summary>
    /// The type argument passed to <c>AmbientContext.Get</c>. Reference types drop the
    /// nullable annotation; nullable value types keep it.
    /// </summary>
    public string StorageType { get; }

    public bool HasGetter { get; }

    public bool HasSetter { get; }

    /// <summary>
    /// True when the property is a non-nullable reference type. A missing value still
    /// reads as null, so the getter needs <c>!</c> to keep generated code free of
    /// nullable warnings in consumers that treat warnings as errors. PHCTX009 warns
    /// about the underlying declaration.
    /// </summary>
    public bool SuppressNullWarning { get; }

    /// <summary>
    /// True when two declarations of the same name can be collapsed rather than
    /// reported as ambiguous.
    /// </summary>
    public bool IsCompatibleWith(AmbientPropertyModel other) =>
        StringComparer.Ordinal.Equals(DeclaredType, other.DeclaredType)
        && HasGetter == other.HasGetter
        && HasSetter == other.HasSetter;

    public bool Equals(AmbientPropertyModel? other) =>
        other is not null
        && StringComparer.Ordinal.Equals(Name, other.Name)
        && StringComparer.Ordinal.Equals(DeclaredType, other.DeclaredType)
        && StringComparer.Ordinal.Equals(StorageType, other.StorageType)
        && HasGetter == other.HasGetter
        && HasSetter == other.HasSetter
        && SuppressNullWarning == other.SuppressNullWarning;

    public override bool Equals(object? obj) => Equals(obj as AmbientPropertyModel);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(Name) ^ StringComparer.Ordinal.GetHashCode(DeclaredType);
}
