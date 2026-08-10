namespace Pondhawk.Context;

/// <summary>
/// Lower-level, key-based access to ambient values, and the place where the active
/// provider is configured.
/// </summary>
/// <remarks>
/// This API exists for framework and infrastructure code. Application code should
/// prefer <see cref="Ambient{T}"/>, which is strongly typed and cannot mistype a key.
/// </remarks>
public static class AmbientContext
{
    private static readonly IAmbientContextProvider DefaultProvider = new ActivityAmbientContextProvider();
    private static readonly AsyncLocal<IAmbientContextProvider?> Scoped = new();

    private static IAmbientContextProvider? _configured;

    /// <summary>
    /// Set the first time <see cref="UseProvider"/> is called, so that the common path
    /// never pays for an <see cref="AsyncLocal{T}"/> read. In production this stays
    /// <see langword="false"/> forever.
    /// </summary>
    private static volatile bool _scopedOverridesEnabled;

    /// <summary>
    /// Gets the provider in effect for the current execution context.
    /// </summary>
    /// <remarks>
    /// Resolution order is scoped override, then configured provider, then the default
    /// <see cref="ActivityAmbientContextProvider"/>.
    /// </remarks>
    public static IAmbientContextProvider Provider
    {
        get
        {
            if (_scopedOverridesEnabled)
            {
                var scoped = Scoped.Value;
                if (scoped is not null)
                    return scoped;
            }

            return _configured ?? DefaultProvider;
        }
    }

    /// <summary>
    /// Selects the provider for the lifetime of the process. Call once during startup.
    /// </summary>
    /// <param name="provider">The provider to use.</param>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A provider has already been configured.</exception>
    /// <remarks>
    /// Configuration is deliberately one-shot. Swapping providers while requests are in
    /// flight would let a request read from a different store than it wrote to.
    /// </remarks>
    public static void Configure(IAmbientContextProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (Interlocked.CompareExchange(ref _configured, provider, null) is not null)
        {
            throw new InvalidOperationException(
                "An ambient context provider has already been configured. " +
                "Configure may only be called once, during application startup. " +
                "For tests, use AmbientContext.UseProvider instead.");
        }
    }

    /// <summary>
    /// Overrides the provider for the current execution context until the returned
    /// scope is disposed. Intended for tests.
    /// </summary>
    /// <param name="provider">The provider to use inside the scope.</param>
    /// <returns>A scope that restores the previous provider when disposed.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// The override is execution-local rather than global, so parallel tests do not
    /// interfere with each other. Calling this method enables an
    /// <see cref="AsyncLocal{T}"/> read on every subsequent context access for the
    /// lifetime of the process, which is why it is not intended for production use.
    /// </remarks>
    public static IDisposable UseProvider(IAmbientContextProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        _scopedOverridesEnabled = true;

        var previous = Scoped.Value;
        Scoped.Value = provider;
        return new ProviderScope(previous);
    }

    /// <summary>
    /// Stores <paramref name="value"/> under <paramref name="key"/>.
    /// </summary>
    /// <typeparam name="T">The value type.</typeparam>
    /// <param name="key">The provider key.</param>
    /// <param name="value">The value to store. Value types are boxed at this boundary.</param>
    public static void Set<T>(string key, T value) => Provider.Set(key, value);

    /// <summary>
    /// Gets the value stored under <paramref name="key"/>, or the default of
    /// <typeparamref name="T"/> when no value is visible.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The provider key.</param>
    /// <exception cref="InvalidCastException">
    /// A value is stored under <paramref name="key"/> but is not a <typeparamref name="T"/>.
    /// This normally means two contexts have collided on a key.
    /// </exception>
    public static T? Get<T>(string key)
    {
        var stored = Provider.Get(key);

        return stored switch
        {
            null => default,
            T value => value,
            _ => throw new InvalidCastException(
                $"Ambient value '{key}' is of type {stored.GetType()}, not {typeof(T)}. " +
                "Two ambient contexts may be using the same property name for different types."),
        };
    }

    /// <summary>
    /// Gets the value stored under <paramref name="key"/> without throwing on a type mismatch.
    /// </summary>
    /// <typeparam name="T">The expected value type.</typeparam>
    /// <param name="key">The provider key.</param>
    /// <param name="value">The value, or the default of <typeparamref name="T"/>.</param>
    /// <returns><see langword="true"/> if a value of type <typeparamref name="T"/> was found.</returns>
    public static bool TryGet<T>(string key, out T? value)
    {
        if (Provider.Get(key) is T stored)
        {
            value = stored;
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>
    /// Returns <see langword="true"/> if a value is visible for <paramref name="key"/>.
    /// </summary>
    /// <param name="key">The provider key.</param>
    public static bool Contains(string key) => Provider.Contains(key);

    /// <summary>
    /// Hides any value for <paramref name="key"/> from the current scope onward.
    /// </summary>
    /// <param name="key">The provider key.</param>
    /// <returns><see langword="true"/> if a value was visible before the call.</returns>
    public static bool Remove(string key) => Provider.Remove(key);

    private sealed class ProviderScope(IAmbientContextProvider? previous) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Scoped.Value = previous;
        }
    }
}
