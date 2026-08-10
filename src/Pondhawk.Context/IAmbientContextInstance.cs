namespace Pondhawk.Context;

/// <summary>
/// Implemented on an ambient context interface by generated code so that
/// <see cref="Ambient{T}"/> can reach the generated implementation with a direct
/// static call.
/// </summary>
/// <typeparam name="TSelf">The ambient context interface itself.</typeparam>
/// <remarks>
/// <para>
/// This is the mechanism that keeps the package free of runtime reflection. The
/// generator emits an explicit implementation of <see cref="Instance"/> on the
/// consumer's interface, so resolving a context is a constrained static call
/// resolved entirely at compile time — no registry, no module initializer, and no
/// startup step that can be forgotten.
/// </para>
/// <para>
/// Application code never implements or references this interface directly.
/// </para>
/// </remarks>
public interface IAmbientContextInstance<TSelf>
    where TSelf : IAmbientContextInstance<TSelf>
{
    /// <summary>
    /// Gets the shared, stateless implementation of the ambient context interface.
    /// </summary>
    static abstract TSelf Instance { get; }
}
