# Implementation Decisions

Decisions taken before implementation began. Where these conflict with
[Pondhawk.Context.md](Pondhawk.Context.md), **these win** — that document is the original
design specification and has not been rewritten.

---

## Runtime semantics

### Storage: `Activity.CustomProperties`, not Baggage

Baggage was considered because child activities inherit it for free. It was rejected on
three grounds:

- Baggage values are `string` only, which would force a serialization layer for `Guid`,
  `int`, records, and classes — the spec's "no serialization required" (line 767) would
  stop being true.
- .NET propagates baggage to downstream services as a W3C `baggage` HTTP header by
  default, so tenant, user description, and audit metadata would leave the process on
  every outbound call, including to third parties.
- W3C caps baggage at roughly 8 KB / 64 entries with silent truncation, and several
  vendors index it into the telemetry backend.

Custom properties plus the parent-chain walk below deliver the inheritance benefit
without any of that.

### Nesting: reads walk the parent chain, writes go to `Activity.Current`

Instrumented libraries — EF Core, `HttpClient` — start their own child activities, and
custom properties are not inherited. Without the walk, the spec's headline EF Core
interceptor example returns `null` for everything.

Writes stay on the current activity, so a value written inside a child shadows the
enclosing one and disappears when that child ends. Nothing outside the current activity
is mutated.

### Keys are bare property names

As specified. Interop across independently declared interfaces is the point of the
`IUserContext` / `IAuditContext` overlap. The cost, accepted knowingly: `Activity`
custom properties are a flat namespace shared with any other library that uses them.

### Type mismatch throws `InvalidCastException`

A mismatch is always a bug or a key collision, and silence makes it expensive to
diagnose. Combined with bare keys, this means a collision with another library is a hard
failure rather than a silent wrong answer.

### Remove and null-writes use a tombstone

`Remove` and `Set(key, null)` write a private sentinel to `Activity.Current` so the
parent-chain walk stops there and returns the default. The ancestor value is untouched
and reappears when the child scope ends.

The sentinel is **required, not stylistic**: `Activity.SetCustomProperty(key, null)`
*deletes* the entry rather than storing null, so writing null directly would un-shadow
the ancestor value instead of hiding it. Verified by test.

### No activity: reads default, writes ignored, no exception

As specified. Library and background code must be safe to call outside a request.

The practical trap, which the spike surfaced and the package README documents:
`new Activity(...).Start()` always sets `Activity.Current`, but
**`ActivitySource.StartActivity()` returns `null` when no listener is registered**. An
app using `ActivitySource` without OpenTelemetry wired up has no activity at all, and
every write silently vanishes. Worth revisiting whether dropped writes should be
observable through an `EventSource` counter.

### Provider configuration is one-shot, with a gated test override

`AmbientContext.Configure(provider)` succeeds once and throws afterwards.
`AmbientContext.UseProvider(provider)` returns an `IDisposable` backed by an
`AsyncLocal`, guarded by a static flag that only flips the first time it is called — so
production never pays for the `AsyncLocal` read. Resolution order is override, then
configured, then default.

This resolves the contradiction between spec lines 532–554 (no unguarded runtime
mutation) and lines 1166–1183 (a disposable `UseProvider`).

The Activity provider defaults with no configuration at all.

---

## Compile time

### Registration: static abstract interface members

**This supersedes the `GeneratedAmbientContexts.Resolve<T>()` design at spec lines
283–311, and retires PHCTX007 as originally defined.**

`Ambient<T>` is constrained to `IAmbientContextInstance<T>`. The generator adds that base
interface and a static `Instance` implementation to the consumer's `partial` interface,
so `Ambient<T>.Current` is a direct static call resolved at compile time — no registry,
no module initializer, no startup step to forget. Using a type that is not an ambient
context is a compile error rather than a runtime throw.

The cost is that every ambient interface must be declared `partial` (PHCTX007, redefined).

This was the one non-obvious assumption in the design, so it was proven with a spike
before any code was written: a runtime library compiled *before* any ambient interface
existed, consumed from a second assembly, published with Native AOT and executed. All
checks passed with zero trim or AOT warnings.

### Reference-type properties should be nullable — warning, not error

A missing value reads as `null`, so `string Tenant { get; }` is a lie. PHCTX009 warns;
the generated getter appends `!` so consuming builds with `TreatWarningsAsErrors` are not
broken by generated code.

### Members that cannot be implemented are errors; the rest are ignored

Methods, events, indexers, and init-only setters are errors — the generated class could
not satisfy them. Default interface implementations and user-declared static members are
left alone and simply not generated, since the interface already supplies them.

### Inheritance: only the leaf interface needs the attribute and `partial`

Base interfaces stay ordinary. Inherited properties are implemented in the derived
context. Identical re-declarations collapse silently; conflicting type or accessors is
PHCTX006.

### Provider-independence analyzer deferred to v2

An analyzer banning direct `Activity` access is hard to scope — `Activity.Current` has
entirely legitimate tracing uses, and an analyzer cannot tell our custom-property keys
from anyone else's.

---

## Build and packaging

| Decision | Value |
|---|---|
| Runtime TFMs | `net8.0` and `net10.0` — consumers are mixed |
| Generator TFM | `netstandard2.0` — required by Roslyn |
| Roslyn pin | **4.8.0**, which sets the .NET 8 SDK as the floor for consuming builds |
| Package shape | One package; generator injected under `analyzers/dotnet/cs` |
| Build | Cake Frosting, matching `pondhawk-rules` |
| Versioning | `version.json` holds `Major.Minor` (1.0); patch is the CI build number |
| CI / feed | GitHub Actions → GitHub Packages; `publish.yml` promotes to nuget.org manually |
| AOT | `IsAotCompatible` on every build; publish-and-run verification on `main` |
| Tests | xUnit v3 + Shouldly + coverlet; Verify snapshots for generator output |

### Deviations from `pondhawk-rules`

- `<Nullable>enable</Nullable>` — nullable annotations are load-bearing here; they drive
  PHCTX009 and the generated `!` suppression.
- `TargetFrameworks` plural on the runtime library.
- xUnit v3 rather than v2, for test isolation in Activity-sensitive tests.
- Two narrowly scoped analyzer suppressions in `.editorconfig`, each with a reason:
  CA1000 (`Ambient<T>.Current` is a static member on a generic type **by design**) and
  CA1716 (`Get`/`Set` are the documented provider API names).

### Generator test harness

`Microsoft.CodeAnalysis.CSharp.SourceGenerators.Testing`'s xUnit verifier package targets
xUnit v2, which conflicts with the xUnit v3 decision. Diagnostics are therefore asserted
directly against a `CSharpGeneratorDriver` run — see `GeneratorHarness` — which also
compiles the generated output and fails if it does not build. Verify handles output
snapshots. This keeps both decisions intact.

---

## Still open

- Whether dropped writes (no current activity) should be observable via `EventSource`.
- Whether `AmbientContext.Get`/`Set` should remain public, given they bypass the type
  safety that `Ambient<T>` provides.
- Strong naming: currently unsigned.
