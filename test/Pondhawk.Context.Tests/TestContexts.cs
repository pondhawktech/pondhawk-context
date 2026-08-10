using Pondhawk.Context;

namespace Pondhawk.Context.Tests;

[AmbientContext]
public partial interface IUserContext
{
    string? UserUid { get; set; }

    string? UserDescription { get; set; }

    int RequestCount { get; set; }

    Guid? CorrelationId { get; set; }
}

[AmbientContext]
public partial interface ITenantContext
{
    string? Tenant { get; set; }
}

/// <summary>
/// A read-only view over keys another context writes. Proves that overlapping property
/// names map to the same provider key.
/// </summary>
[AmbientContext]
public partial interface IAuditContext
{
    string? UserUid { get; }

    string? UserDescription { get; }

    string? Tenant { get; }
}

public interface IUserIdentity
{
    string? UserUid { get; }
}

/// <summary>
/// Inherits from an interface that is neither attributed nor partial, which is the
/// supported inheritance shape.
/// </summary>
[AmbientContext]
public partial interface IInheritingContext : IUserIdentity
{
    string? UserDescription { get; set; }
}
