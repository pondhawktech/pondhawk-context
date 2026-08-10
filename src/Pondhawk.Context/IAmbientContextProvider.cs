namespace Pondhawk.Context;

/// <summary>
/// Abstracts where ambient values are stored. Implement this only when building
/// infrastructure; application code uses <see cref="Ambient{T}"/>.
/// </summary>
/// <remarks>
/// Implementations must be safe for concurrent use and must isolate values per logical
/// operation. Values are stored as <see cref="object"/>; no serialization is performed.
/// </remarks>
public interface IAmbientContextProvider
{
    /// <summary>
    /// Gets the stored value for <paramref name="key"/>, or <see langword="null"/> if
    /// no value is visible in the current scope.
    /// </summary>
    /// <param name="key">The provider key.</param>
    object? Get(string key);

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/> in the current scope.
    /// </summary>
    /// <param name="key">The provider key.</param>
    /// <param name="value">
    /// The value to store. A <see langword="null"/> value must hide any value visible
    /// from an enclosing scope rather than reveal it.
    /// </param>
    void Set(string key, object? value);

    /// <summary>
    /// Returns <see langword="true"/> if a value is visible for <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The provider key.</param>
    bool Contains(string key);

    /// <summary>
    /// Hides any value for <paramref name="key"/> from the current scope onward.
    /// </summary>
    /// <param name="key">The provider key.</param>
    /// <returns><see langword="true"/> if a value was visible before the call.</returns>
    bool Remove(string key);
}
