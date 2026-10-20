using System.Diagnostics;
using Pondhawk.Context;
using Pondhawk.Context.AotTests;

// Verifies that the compile-time resolution behind Ambient<T> survives Native AOT.
// The risk this guards is specific: AOT cannot generate code at runtime, so the
// constrained static-virtual call used to reach the generated implementation must be
// fully resolvable at publish time.

var failures = 0;

void Check(string name, bool condition)
{
    Console.WriteLine($"{(condition ? "PASS" : "FAIL")}  {name}");
    if (!condition)
        failures++;
}

Check("no activity: read returns default", Ambient<IUserContext>.Current.UserUid is null);

using (var root = new Activity("root").Start())
{
    Ambient<IUserContext>.Current.UserUid = "alice";
    Ambient<IUserContext>.Current.RequestCount = 7;
    Ambient<ITenantContext>.Current.Tenant = "acme";

    Check("set/get round trip", Ambient<IUserContext>.Current.UserUid == "alice");
    Check("value type round trip", Ambient<IUserContext>.Current.RequestCount == 7);
    Check("singleton identity", ReferenceEquals(Ambient<IUserContext>.Current, Ambient<IUserContext>.Current));
    Check("read-only view shares keys", Ambient<IAuditContext>.Current.UserUid == "alice");

    using (var child = new Activity("child").Start())
    {
        Check("child reads ancestor value", Ambient<IUserContext>.Current.UserUid == "alice");

        Ambient<IUserContext>.Current.UserUid = "shadowed";
        Check("child write shadows ancestor", Ambient<IUserContext>.Current.UserUid == "shadowed");
    }

    Check("ancestor value survives child scope", Ambient<IUserContext>.Current.UserUid == "alice");

    await Task.Yield();
    Check("value flows across await", Ambient<IUserContext>.Current.UserUid == "alice");
}

Console.WriteLine();
Console.WriteLine(failures == 0
    ? "Native AOT verification passed."
    : $"Native AOT verification failed: {failures} check(s).");

return failures == 0 ? 0 : 1;
