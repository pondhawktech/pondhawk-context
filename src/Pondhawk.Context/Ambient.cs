namespace Pondhawk.Context;

/// <summary>
/// The primary entry point for reading and writing ambient context values.
/// </summary>
/// <typeparam name="T">
/// An interface marked with <see cref="AmbientContextAttribute"/>. The constraint is
/// satisfied by generated code, so passing a type that is not an ambient context is a
/// compile-time error rather than a runtime failure.
/// </typeparam>
/// <example>
/// <code>
/// Ambient&lt;IUserContext&gt;.Current.UserUid = "alice";
/// var uid = Ambient&lt;IUserContext&gt;.Current.UserUid;
/// </code>
/// </example>
public static class Ambient<T>
    where T : IAmbientContextInstance<T>
{
    /// <summary>
    /// Gets the ambient context implementation for <typeparamref name="T"/>.
    /// </summary>
    /// <remarks>
    /// The returned object is a stateless singleton shared by every caller; request
    /// isolation comes from the active <see cref="IAmbientContextProvider"/>, not from
    /// this object. It is never <see langword="null"/>.
    /// </remarks>
    public static T Current => T.Instance;
}
