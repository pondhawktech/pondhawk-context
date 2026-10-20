using System.Diagnostics;
using Shouldly;

namespace Pondhawk.Context.Tests;

public sealed class AmbientContextTests
{
    [Fact]
    public void SetThenGet_RoundTrips()
    {
        using var scope = new Activity("root").Start();

        Ambient<IUserContext>.Current.UserUid = "alice";

        Ambient<IUserContext>.Current.UserUid.ShouldBe("alice");
    }

    [Fact]
    public void Current_IsAStatelessSingleton()
    {
        Ambient<IUserContext>.Current.ShouldBeSameAs(Ambient<IUserContext>.Current);
    }

    [Fact]
    public void OverlappingInterfaces_ShareProviderKeys()
    {
        using var scope = new Activity("root").Start();

        Ambient<IUserContext>.Current.UserUid = "alice";
        Ambient<ITenantContext>.Current.Tenant = "acme";

        Ambient<IAuditContext>.Current.UserUid.ShouldBe("alice");
        Ambient<IAuditContext>.Current.Tenant.ShouldBe("acme");
    }

    [Fact]
    public void InheritedProperties_AreImplemented()
    {
        using var scope = new Activity("root").Start();

        Ambient<IUserContext>.Current.UserUid = "alice";

        Ambient<IInheritingContext>.Current.UserUid.ShouldBe("alice");
    }

    [Fact]
    public void MissingValueType_ReturnsDefault()
    {
        using var scope = new Activity("root").Start();

        Ambient<IUserContext>.Current.RequestCount.ShouldBe(0);
        Ambient<IUserContext>.Current.CorrelationId.ShouldBeNull();
    }

    [Fact]
    public void ValueTypes_RoundTripThroughBoxing()
    {
        using var scope = new Activity("root").Start();
        var correlationId = Guid.NewGuid();

        Ambient<IUserContext>.Current.RequestCount = 42;
        Ambient<IUserContext>.Current.CorrelationId = correlationId;

        Ambient<IUserContext>.Current.RequestCount.ShouldBe(42);
        Ambient<IUserContext>.Current.CorrelationId.ShouldBe(correlationId);
    }

    [Fact]
    public void TypeMismatch_Throws()
    {
        using var scope = new Activity("root").Start();
        AmbientContext.Set("Mismatch", 123);

        Should.Throw<InvalidCastException>(() => AmbientContext.Get<string>("Mismatch"));
    }

    [Fact]
    public void TryGet_DoesNotThrowOnMismatch()
    {
        using var scope = new Activity("root").Start();
        AmbientContext.Set("Mismatch", 123);

        AmbientContext.TryGet<string>("Mismatch", out var value).ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public async Task Values_FlowAcrossAwait()
    {
        using var scope = new Activity("root").Start();
        Ambient<IUserContext>.Current.UserUid = "alice";

        await Task.Yield();

        Ambient<IUserContext>.Current.UserUid.ShouldBe("alice");
    }

    [Fact]
    public async Task ConcurrentScopes_StayIsolated()
    {
        var results = await Task.WhenAll(Enumerable.Range(0, 64).Select(async index =>
        {
            using var scope = new Activity($"request-{index}").Start();
            Ambient<IUserContext>.Current.UserUid = $"user-{index}";

            await Task.Yield();

            using var inner = new Activity($"request-{index}-inner").Start();
            await Task.Delay(1);

            return Ambient<IUserContext>.Current.UserUid == $"user-{index}";
        }));

        results.ShouldAllBe(result => result);
    }
}
