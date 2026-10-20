using System.Diagnostics;
using Shouldly;

namespace Pondhawk.Context.Tests;

/// <summary>
/// Behaviour specific to the Activity provider: the parent-chain walk, shadowing, and
/// what happens with no ambient scope at all.
/// </summary>
public sealed class ActivityProviderTests
{
    [Fact]
    public void ChildActivity_ReadsAncestorValue()
    {
        using var root = new Activity("root").Start();
        Ambient<IUserContext>.Current.UserUid = "alice";

        using var child = new Activity("child").Start();

        // Without the parent walk this is null, and every EF Core interceptor breaks.
        Ambient<IUserContext>.Current.UserUid.ShouldBe("alice");
    }

    [Fact]
    public void ChildActivity_WriteShadowsAncestorAndDoesNotMutateIt()
    {
        using var root = new Activity("root").Start();
        Ambient<IUserContext>.Current.UserUid = "alice";

        using (var child = new Activity("child").Start())
        {
            Ambient<IUserContext>.Current.UserUid = "shadowed";
            Ambient<IUserContext>.Current.UserUid.ShouldBe("shadowed");
        }

        Ambient<IUserContext>.Current.UserUid.ShouldBe("alice");
    }

    [Fact]
    public void Remove_InChild_HidesAncestorWithoutMutatingIt()
    {
        using var root = new Activity("root").Start();
        Ambient<IUserContext>.Current.UserUid = "alice";

        using (var child = new Activity("child").Start())
        {
            AmbientContext.Remove("UserUid").ShouldBeTrue();
            AmbientContext.Contains("UserUid").ShouldBeFalse();
            Ambient<IUserContext>.Current.UserUid.ShouldBeNull();
        }

        Ambient<IUserContext>.Current.UserUid.ShouldBe("alice");
    }

    [Fact]
    public void SettingNull_ShadowsRatherThanRevealingAncestor()
    {
        using var root = new Activity("root").Start();
        Ambient<IUserContext>.Current.UserUid = "alice";

        using var child = new Activity("child").Start();
        Ambient<IUserContext>.Current.UserUid = null;

        // Activity.SetCustomProperty(key, null) deletes the entry, so a sentinel is
        // required here — otherwise the ancestor value would reappear.
        Ambient<IUserContext>.Current.UserUid.ShouldBeNull();
    }

    [Fact]
    public void NoActivity_ReadsDefaultAndIgnoresWrites()
    {
        Activity.Current.ShouldBeNull();

        Should.NotThrow(() => Ambient<IUserContext>.Current.UserUid = "ignored");

        Ambient<IUserContext>.Current.UserUid.ShouldBeNull();
        AmbientContext.Contains("UserUid").ShouldBeFalse();
        AmbientContext.Remove("UserUid").ShouldBeFalse();
    }

    [Fact]
    public void UseProvider_OverridesForTheCurrentScopeOnly()
    {
        var provider = new DictionaryAmbientContextProvider();

        using (AmbientContext.UseProvider(provider))
        {
            Ambient<ITenantContext>.Current.Tenant = "acme";

            // No Activity was started, so this can only have come from the override.
            Ambient<ITenantContext>.Current.Tenant.ShouldBe("acme");
            provider.Values.ShouldContainKey("Tenant");
        }

        AmbientContext.Provider.ShouldBeOfType<ActivityAmbientContextProvider>();
    }

    private sealed class DictionaryAmbientContextProvider : IAmbientContextProvider
    {
        public Dictionary<string, object?> Values { get; } = new(StringComparer.Ordinal);

        public object? Get(string key) => Values.TryGetValue(key, out var value) ? value : null;

        public void Set(string key, object? value) => Values[key] = value;

        public bool Contains(string key) => Values.ContainsKey(key) && Values[key] is not null;

        public bool Remove(string key) => Values.Remove(key);
    }
}
