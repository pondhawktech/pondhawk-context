using System.Diagnostics;

namespace Pondhawk.Context;

/// <summary>
/// The default provider. Stores values in
/// <see cref="Activity.SetCustomProperty(string, object?)"/> on the current
/// <see cref="Activity"/>, and resolves reads by walking up the
/// <see cref="Activity.Parent"/> chain.
/// </summary>
/// <remarks>
/// <para>
/// Reads walk the parent chain because instrumented libraries — EF Core, HttpClient,
/// and others — start their own child activities. Custom properties are not inherited,
/// so without the walk a value written in middleware would be invisible inside an
/// EF Core interceptor.
/// </para>
/// <para>
/// Writes go to <see cref="Activity.Current"/> only, so a value written inside a child
/// activity shadows the enclosing one and disappears when that child ends. Nothing
/// outside the current activity is ever mutated.
/// </para>
/// <para>
/// Custom properties are used rather than baggage or tags: baggage is propagated to
/// downstream services as an HTTP header, and ambient values such as tenant, user
/// description, and audit metadata must not leave the process.
/// </para>
/// <para>
/// If <see cref="Activity.Current"/> is <see langword="null"/>, reads return
/// <see langword="null"/> and writes are ignored. See the README for the
/// <c>ActivitySource</c> caveat.
/// </para>
/// </remarks>
public sealed class ActivityAmbientContextProvider : IAmbientContextProvider
{
    /// <summary>
    /// Marks a key as explicitly absent so that the parent-chain walk stops instead of
    /// revealing an enclosing activity's value.
    /// </summary>
    /// <remarks>
    /// A sentinel is required rather than merely tidy: <see cref="Activity.SetCustomProperty"/>
    /// <em>removes</em> the entry when passed <see langword="null"/>, which would
    /// un-shadow the ancestor value instead of hiding it.
    /// </remarks>
    private sealed class Tombstone
    {
        internal static readonly Tombstone Instance = new();

        private Tombstone()
        {
        }
    }

    /// <inheritdoc />
    public object? Get(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        for (var activity = Activity.Current; activity is not null; activity = activity.Parent)
        {
            var stored = activity.GetCustomProperty(key);
            if (stored is null)
                continue;

            return ReferenceEquals(stored, Tombstone.Instance) ? null : stored;
        }

        return null;
    }

    /// <inheritdoc />
    public void Set(string key, object? value)
    {
        ArgumentNullException.ThrowIfNull(key);

        Activity.Current?.SetCustomProperty(key, value ?? Tombstone.Instance);
    }

    /// <inheritdoc />
    public bool Contains(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        for (var activity = Activity.Current; activity is not null; activity = activity.Parent)
        {
            var stored = activity.GetCustomProperty(key);
            if (stored is null)
                continue;

            return !ReferenceEquals(stored, Tombstone.Instance);
        }

        return false;
    }

    /// <inheritdoc />
    public bool Remove(string key)
    {
        ArgumentNullException.ThrowIfNull(key);

        var activity = Activity.Current;
        if (activity is null)
            return false;

        var existed = Contains(key);
        activity.SetCustomProperty(key, Tombstone.Instance);
        return existed;
    }
}
