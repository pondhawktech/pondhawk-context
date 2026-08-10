namespace Pondhawk.Context;

/// <summary>
/// Marks an interface as an ambient context. The source generator emits a concrete
/// implementation whose properties read and write the active
/// <see cref="IAmbientContextProvider"/>, and wires the interface up so it can be
/// resolved through <see cref="Ambient{T}"/>.
/// </summary>
/// <remarks>
/// The interface must be declared <c>partial</c>: the generator adds a base interface
/// and a static implementation to it. It may declare properties only.
/// </remarks>
/// <example>
/// <code>
/// [AmbientContext]
/// public partial interface IUserContext
/// {
///     string? UserUid { get; set; }
/// }
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class AmbientContextAttribute : Attribute;
