# Pondhawk.Context

Strongly typed, async-flowing ambient application context for .NET — without dependency
injection, without passing parameters through every layer, and without runtime
reflection or proxies.

```csharp
[AmbientContext]
public partial interface IUserContext
{
    string? UserUid { get; set; }
    string? UserDescription { get; set; }
}

// In middleware
Ambient<IUserContext>.Current.UserUid = principal.GetUserUid();

// Anywhere downstream in the same execution flow — an EF Core interceptor, say
var uid = Ambient<IUserContext>.Current.UserUid;
```

No registration, no service collection, no startup call. Install the package, mark an
interface `partial`, and use it.

## How it works

A source generator implements each `[AmbientContext]` interface at compile time. The
implementation is a stateless singleton whose properties read and write the active
provider; request isolation comes from the provider, not from the object.

By default values live in `Activity.Current`'s custom properties. Reads walk up the
`Activity.Parent` chain, so a value set in middleware is still visible inside a child
activity started by EF Core or `HttpClient`. Writes go to the current activity only, so
a value written inside a child shadows the enclosing one and disappears when that child
ends — nothing outside the current activity is ever mutated.

Resolution is entirely compile-time: `Ambient<T>.Current` is a direct static call. There
is no registry, no reflection, and no module initializer, which is what makes the package
safe under trimming and Native AOT. Using a type that is not an ambient context is a
compile error, not a runtime one.

## Read-only views

Different interfaces can expose the same underlying keys, because a provider key is just
the property name:

```csharp
[AmbientContext]
public partial interface IAuditContext
{
    string? UserUid { get; }        // written through IUserContext, read-only here
    string? Tenant { get; }
}
```

## Important: you need an Activity

The default provider stores values on `Activity.Current`. **If there is no current
activity, reads return defaults and writes are silently ignored.** This keeps library
code safe to call outside a request, but it is also the most likely reason for
"it isn't working":

- `new Activity("name").Start()` always sets `Activity.Current`.
- **`ActivitySource.StartActivity()` returns `null` when no listener is registered.**
  An app that creates activities through an `ActivitySource` without OpenTelemetry (or
  another listener) wired up has no current activity at all, and every ambient write
  will vanish.

ASP.NET Core creates an activity per request when hosting diagnostics are enabled, which
is the normal case. If you are writing a worker, a test, or a console app, start an
activity yourself.

## Supported property types

Values are stored as objects with no serialization, so any type works: `string`, `Guid`,
`int`, `DateTimeOffset`, enums, records, classes, and their nullable forms. Value types
are boxed at the provider boundary.

Reference-type properties should be declared nullable — a missing value reads as `null`,
and the generator warns (`PHCTX009`) when they are not.

## Diagnostics

| ID | Meaning |
|----|---------|
| PHCTX001 | Ambient context interfaces may contain properties only |
| PHCTX002 | Ambient context type must be an interface |
| PHCTX003 | Indexers are not supported |
| PHCTX004 | Events are not supported |
| PHCTX005 | Generic ambient context interfaces are not supported |
| PHCTX006 | Duplicate or ambiguous inherited properties were detected |
| PHCTX007 | Ambient context interfaces must be declared `partial` |
| PHCTX008 | Init-only setters are not supported |
| PHCTX009 | Ambient context reference properties should be nullable (warning) |
| PHCTX010 | Ambient contexts require C# 11 or later |
| PHCTX011 | Nested ambient context interfaces are not supported |

## Choosing a different provider

```csharp
AmbientContext.Configure(new MyProvider());   // once, at startup
```

Configuration is one-shot by design. For tests, `AmbientContext.UseProvider(provider)`
returns a scope that overrides the provider for the current execution context only.

## Not an authorization boundary

Ambient context describes the current operation. Authorization decisions should continue
to rely on your application's trusted identity and authorization mechanisms.

## Requirements

- .NET 8 or .NET 10
- C# 11 or later
- .NET 8 SDK or later to build

MIT licensed. © Pond Hawk Technologies Inc.
