using Pondhawk.Context;

namespace Pondhawk.Context.AotTests;

[AmbientContext]
public partial interface IUserContext
{
    string? UserUid { get; set; }

    int RequestCount { get; set; }
}

[AmbientContext]
public partial interface ITenantContext
{
    string? Tenant { get; set; }
}

[AmbientContext]
public partial interface IAuditContext
{
    string? UserUid { get; }

    string? Tenant { get; }
}
