using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Pondhawk.Context.Generator.Models;

/// <summary>
/// A source location captured by value so that models stay comparable and the
/// incremental pipeline can cache them. <see cref="Location"/> itself holds a reference
/// to a syntax tree, which would defeat caching.
/// </summary>
internal sealed class LocationInfo : IEquatable<LocationInfo>
{
    private LocationInfo(string filePath, TextSpan textSpan, LinePositionSpan lineSpan)
    {
        FilePath = filePath;
        TextSpan = textSpan;
        LineSpan = lineSpan;
    }

    public string FilePath { get; }

    public TextSpan TextSpan { get; }

    public LinePositionSpan LineSpan { get; }

    public static LocationInfo? From(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault(candidate => candidate.IsInSource);
        return location is null ? null : From(location);
    }

    public static LocationInfo? From(Location location)
    {
        if (!location.IsInSource || location.SourceTree is null)
            return null;

        return new LocationInfo(
            location.SourceTree.FilePath,
            location.SourceSpan,
            location.GetLineSpan().Span);
    }

    public Location ToLocation() => Location.Create(FilePath, TextSpan, LineSpan);

    public bool Equals(LocationInfo? other) =>
        other is not null
        && StringComparer.Ordinal.Equals(FilePath, other.FilePath)
        && TextSpan == other.TextSpan
        && LineSpan == other.LineSpan;

    public override bool Equals(object? obj) => Equals(obj as LocationInfo);

    public override int GetHashCode() =>
        StringComparer.Ordinal.GetHashCode(FilePath) ^ TextSpan.GetHashCode();
}
