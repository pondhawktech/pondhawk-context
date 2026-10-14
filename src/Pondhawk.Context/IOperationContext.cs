namespace Pondhawk.Context;

/// <summary>
/// What every Pondhawk library may assume about the current unit of work: which one it is, who
/// it is for, and which tenant it belongs to.
/// </summary>
/// <remarks>
/// <para>
/// Every other ambient context is declared by the application that needs it. This one is shipped
/// with the package, because the things it holds are read by libraries that know nothing of each
/// other: a log sink stamps them on every event, an audit journal records them on every change.
/// One declaration means they read the same values, written once.
/// </para>
/// <para>
/// <b>Who writes it.</b> In a web application, request middleware — Pondhawk.Logging's does — sets
/// all four from the request and its caller, and <see cref="Operation.Anonymous"/> as the subject
/// when nobody is signed in. Outside a request, <see cref="Operation.Begin"/> starts a unit of work
/// with a fresh correlation id and <see cref="Operation.System"/> as the subject. Code deeper in may
/// set the subject itself when it learns who is acting, as a sign-in page does.
/// </para>
/// <para>
/// <b>None of it leaves the process.</b> Like every ambient value it is a custom property of the
/// current <see cref="System.Diagnostics.Activity"/>, not baggage (see DECISIONS.md), so it is never
/// sent on an outbound call. A correlation id meant for another service is sent there on purpose,
/// in a header.
/// </para>
/// <para>
/// The keys are the bare property names, as for every context. A context of the application's own
/// with a property of the same name and another type collides with this one, and reading it throws.
/// </para>
/// </remarks>
[AmbientContext]
public partial interface IOperationContext
{
    /// <summary>The unit of work's identifier, shared by everything logged or recorded within it.</summary>
    string? CorrelationId { get; set; }

    /// <summary>
    /// Who the unit of work is for, by an identifier that does not change: a user's subject
    /// identifier, or a client's id when a machine is acting for itself. <see cref="Operation.System"/>
    /// for the application's own work; <see cref="Operation.Anonymous"/> for a caller nobody knows.
    /// </summary>
    string? SubjectId { get; set; }

    /// <summary>Who the unit of work is for, as a person reading a log or a trail would know them: a user name, or a client's id.</summary>
    string? SubjectName { get; set; }

    /// <summary>The tenant the unit of work belongs to, when the application has tenants.</summary>
    string? Tenant { get; set; }
}
