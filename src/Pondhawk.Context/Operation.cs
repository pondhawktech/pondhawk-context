using System.Diagnostics;
using System.Globalization;

namespace Pondhawk.Context;

/// <summary>Starts a unit of work outside a request, and names the subjects that are nobody in particular.</summary>
public static class Operation
{
    /// <summary>
    /// The subject of the application's own work, with no caller behind it: a scheduled job, a queue
    /// consumer, a migration.
    /// </summary>
    public const string System = "system";

    /// <summary>
    /// The subject of a request from someone who has not shown who they are: a sign-in attempt, a
    /// request for a password reset. Not <see cref="System"/>: a person may well be behind it.
    /// </summary>
    public const string Anonymous = "anonymous";

    /// <summary>
    /// Begins a unit of work: starts an <see cref="Activity"/>, gives it a correlation id, and makes
    /// <see cref="System"/> its subject. Dispose the result when the work is done.
    /// </summary>
    /// <param name="correlationId">The correlation id to use; null for a fresh one.</param>
    /// <param name="name">The activity's operation name.</param>
    /// <returns>The started activity, which ends the unit of work when disposed.</returns>
    /// <remarks>
    /// <para>
    /// For background work, which has no request to start an activity for it and would otherwise
    /// have no context at all: every ambient write would be dropped.
    /// </para>
    /// <para>
    /// Begun inside another unit of work, it is a child of it: its values shadow the enclosing ones
    /// until it is disposed, and anything it does not set is read through from the parent.
    /// </para>
    /// </remarks>
    public static Activity Begin(string? correlationId = null, string name = "Operation")
    {
        var activity = new Activity(name).Start();
        var context = Ambient<IOperationContext>.Current;

        context.CorrelationId = correlationId ?? NewCorrelationId();
        context.SubjectId = System;
        context.SubjectName = System;

        return activity;
    }

    /// <summary>A fresh correlation id: 32 hexadecimal digits, which any header or log field will carry as it is.</summary>
    /// <returns>The id.</returns>
    public static string NewCorrelationId() => Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
}
