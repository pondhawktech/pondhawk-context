# Pondhawk.Context

## Product Specification

**Project Name:** Pondhawk.Context

**Target Framework:** .NET 10+

**Package Type:** Standalone NuGet Package

**Dependencies:** BCL only at runtime

**Compiler Component:** Roslyn Incremental Source Generator

**Status:** Design Specification

---

# Overview

Pondhawk.Context provides a lightweight, strongly typed ambient context framework for .NET applications.

It allows any component executing within the current asynchronous execution flow to read and write request-scoped values without:

- Dependency Injection
- Static global state containing request data
- Direct `AsyncLocal<T>` usage in application code
- Passing context parameters through multiple layers
- Runtime-generated proxies
- Runtime reflection in the normal execution path

The default implementation stores values inside:

```csharp
System.Diagnostics.Activity.Current.CustomProperties
```

The storage mechanism is hidden behind a provider abstraction.

Strongly typed context interfaces are implemented at compile time using a Roslyn Incremental Source Generator.

The package is intended to become the standard mechanism for request-scoped application context across Pondhawk projects.

---

# Core Design Principles

Pondhawk.Context must be:

- Strongly typed
- Provider independent
- Request scoped
- Async safe
- Native AOT friendly
- Trimming friendly
- Free of runtime proxy generation
- Free of runtime reflection in normal property access
- Minimal in runtime dependencies
- Suitable for infrastructure code such as EF Core interceptors

---

# Primary Use Cases

Typical ambient values include:

- Current Tenant
- Current User UID
- Current User Description
- Request Correlation Identifier
- Audit Metadata
- Feature Flags
- Request Metadata

Typical consumers include:

- EF Core interceptors
- ASP.NET Core middleware
- Audit providers
- Validation
- Authorization infrastructure
- Logging enrichment
- Request processing infrastructure
- Background operations that establish their own Activity

---

# High-Level Architecture

```text
Application Code

        │

        ▼

Ambient<IUserContext>.Current

        │

        ▼

Generated IUserContext Implementation

        │

        ▼

AmbientContext

        │

        ▼

IAmbientContextProvider

        │

        ▼

ActivityAmbientContextProvider

        │

        ▼

Activity.Current.CustomProperties
```

There is no runtime proxy generation.

---

# Package Structure

The implementation should be divided logically into two components.

```text
Pondhawk.Context
    Runtime library

Pondhawk.Context.Generator
    Roslyn Incremental Source Generator
```

The preferred distribution model is a single consumer-facing NuGet package:

```xml
<PackageReference Include="Pondhawk.Context" Version="..." />
```

The source generator should be packaged as an analyzer/compiler asset so consumers do not need to reference it separately.

---

# Runtime Library

The runtime library contains:

```text
Ambient<T>
AmbientContext
IAmbientContextProvider
ActivityAmbientContextProvider
AmbientContextAttribute
Supporting exceptions
Supporting abstractions
```

It must not depend on Roslyn packages at runtime.

---

# Source Generator

The generator scans the consumer compilation for interfaces marked with:

```csharp
[AmbientContext]
```

Example:

```csharp
[AmbientContext]
public interface IUserContext
{
    string? UserUid { get; set; }

    string? UserDescription { get; set; }
}
```

Another example:

```csharp
[AmbientContext]
public interface ITenantContext
{
    string? Tenant { get; set; }
}
```

The generator creates concrete implementations of those interfaces during compilation.

---

# Generated Implementation

Given:

```csharp
[AmbientContext]
public interface IUserContext
{
    string? UserUid { get; set; }

    string? UserDescription { get; set; }
}
```

The generator should create code logically equivalent to:

```csharp
internal sealed class UserContextGenerated : IUserContext
{
    public string? UserUid
    {
        get => AmbientContext.Get<string>(nameof(UserUid));
        set => AmbientContext.Set(nameof(UserUid), value);
    }

    public string? UserDescription
    {
        get => AmbientContext.Get<string>(nameof(UserDescription));
        set => AmbientContext.Set(nameof(UserDescription), value);
    }
}
```

The exact generated type name is an implementation detail.

Application code must never reference the generated implementation directly.

---

# Public API

## Ambient<T>

The primary strongly typed API is:

```csharp
Ambient<T>.Current
```

Example:

```csharp
var user = Ambient<IUserContext>.Current;

user.UserUid = "alice";
user.UserDescription = "James Moring";
```

Later in the same execution context:

```csharp
var userUid =
    Ambient<IUserContext>.Current.UserUid;
```

The returned implementation is generated at compile time.

---

# Generated Registration

The generator must also generate the mapping between interface types and their concrete implementations.

Conceptually:

```csharp
internal static class GeneratedAmbientContexts
{
    internal static T Resolve<T>()
        where T : class
    {
        if (typeof(T) == typeof(IUserContext))
            return (T)(object)UserContextInstance;

        if (typeof(T) == typeof(ITenantContext))
            return (T)(object)TenantContextInstance;

        throw new InvalidOperationException(
            $"No generated ambient context exists for {typeof(T)}.");
    }

    private static readonly IUserContext UserContextInstance =
        new UserContextGenerated();

    private static readonly ITenantContext TenantContextInstance =
        new TenantContextGenerated();
}
```

The actual generated implementation may use a more efficient mechanism.

The important requirement is:

> Resolving `Ambient<T>.Current` must not require runtime reflection, expression compilation, emitted IL, or dynamic proxy generation.

---

# Singleton Generated Implementations

Generated interface implementations should normally be cached as static singleton objects.

This is safe because generated implementations contain no request-specific state.

For example:

```text
UserContextGenerated
```

does not hold:

```text
UserUid
UserDescription
Tenant
```

as instance fields.

Instead, every property getter and setter accesses the current provider.

Therefore:

```text
one generated IUserContext object
```

may safely serve all concurrent requests.

The request data itself remains isolated by the underlying provider.

---

# AmbientContext

`AmbientContext` provides lower-level access to the provider.

Example:

```csharp
AmbientContext.Set("Foo", 123);

var value = AmbientContext.Get<int>("Foo");
```

This API is primarily intended for:

- Framework code
- Internal infrastructure
- Dynamic scenarios

Normal application code should prefer:

```csharp
Ambient<T>.Current
```

---

# AmbientContext API

Minimum API:

```csharp
public static class AmbientContext
{
    public static void Set<T>(
        string key,
        T value);

    public static T? Get<T>(
        string key);

    public static bool TryGet<T>(
        string key,
        out T? value);

    public static bool Contains(
        string key);

    public static bool Remove(
        string key);
}
```

The exact signatures may be refined during implementation.

---

# Provider Model

Storage is abstracted through:

```csharp
public interface IAmbientContextProvider
{
    object? Get(string key);

    void Set(string key, object? value);

    bool Contains(string key);

    bool Remove(string key);
}
```

Application code must not access providers directly unless implementing infrastructure.

---

# Default Provider

## ActivityAmbientContextProvider

The default provider stores values in:

```csharp
Activity.Current.CustomProperties
```

Reads use:

```csharp
Activity.Current?.GetCustomProperty(key)
```

Writes use:

```csharp
Activity.Current?.SetCustomProperty(key, value)
```

The Activity provider must not use:

- Activity Tags
- Activity Baggage
- Activity Events

Ambient values are application context, not necessarily telemetry.

---

# Why Activity

`Activity.Current` provides a natural ambient execution context in modern .NET applications.

It already flows through:

```text
async
await
Task
ASP.NET Core request execution
EF Core calls
```

This avoids maintaining a second independent ambient execution mechanism when Activity already exists.

---

# Provider Independence

Application code must never contain:

```csharp
Activity.Current
```

for Pondhawk.Context values.

It must use:

```csharp
Ambient<T>.Current
```

or:

```csharp
AmbientContext
```

This permits future storage implementations without changing consuming code.

---

# Future Providers

Possible implementations include:

## AsyncLocalAmbientContextProvider

Useful for environments where Activity is not available.

---

## CompositeAmbientContextProvider

Allows multiple stores to participate.

---

## TestAmbientContextProvider

Provides deterministic in-memory context for unit tests.

---

## Custom Host Providers

Applications may eventually provide custom implementations if required.

---

# Provider Selection

Provider selection should be treated as application-startup configuration.

The active provider should not normally be replaceable while requests are executing.

Avoid exposing unrestricted runtime mutation such as:

```csharp
AmbientContext.Provider = ...
```

unless carefully guarded.

Preferred designs include:

```csharp
AmbientContext.Configure(...)
```

or one-time initialization.

The API should prevent accidental provider replacement after initialization.

---

# Interface Model

Ambient context definitions are ordinary interfaces marked with:

```csharp
[AmbientContext]
```

Example:

```csharp
[AmbientContext]
public interface IUserContext
{
    string? UserUid { get; set; }

    string? UserDescription { get; set; }
}
```

---

# Multiple Views Over Shared Context

Different interfaces can expose overlapping parts of the same ambient context.

Example:

```csharp
[AmbientContext]
public interface IUserContext
{
    string? UserUid { get; set; }

    string? UserDescription { get; set; }
}

[AmbientContext]
public interface IAuditContext
{
    string? UserUid { get; }

    string? UserDescription { get; }
}
```

Both map to the same provider keys:

```text
UserUid
UserDescription
```

Therefore:

```csharp
Ambient<IUserContext>.Current.UserUid = "alice";
```

can later be read through:

```csharp
var uid =
    Ambient<IAuditContext>.Current.UserUid;
```

---

# Property Naming

Version 1 should map provider keys directly from property names.

Example:

```text
Property:
UserUid

Provider Key:
UserUid
```

This makes independently defined interfaces interoperable when their property names match.

---

# Future Property Naming Attribute

Future versions may support explicit aliases:

```csharp
[AmbientName("UserUid")]
string? CurrentUser { get; }
```

This is not required for version 1.

---

# Supported Interface Members

Version 1 ambient interfaces should support only properties.

Supported:

```csharp
string? UserUid { get; set; }

string? Tenant { get; }

Guid RequestId { get; set; }
```

Not supported:

```csharp
void Save();

event EventHandler Changed;

string this[int index] { get; }
```

Unsupported constructs should produce compile-time diagnostics.

---

# Source Generator Diagnostics

The generator should fail early with clear diagnostics.

Suggested diagnostic identifiers follow the prefix:

```text
PHCTX
```

Example diagnostics:

## PHCTX001

Ambient context interfaces may contain properties only.

---

## PHCTX002

Ambient context type must be an interface.

---

## PHCTX003

Indexers are not supported.

---

## PHCTX004

Events are not supported.

---

## PHCTX005

Generic ambient context interfaces are not supported in version 1.

---

## PHCTX006

Duplicate or ambiguous inherited properties were detected.

---

## PHCTX007

Ambient interface could not be registered.

---

# Interface Inheritance

Simple interface inheritance should be supported when possible.

Example:

```csharp
public interface IUserIdentityContext
{
    string? UserUid { get; }
}

[AmbientContext]
public interface IUserContext :
    IUserIdentityContext
{
    string? UserDescription { get; set; }
}
```

The generated implementation should implement inherited properties.

Ambiguous inheritance should produce a compiler diagnostic rather than runtime failure.

---

# Supported Property Types

Because Activity custom properties store objects directly, Pondhawk.Context does not require serialization.

Version 1 should support:

- string
- Guid
- nullable Guid
- bool
- nullable bool
- byte
- short
- int
- long
- float
- double
- decimal
- nullable value types
- DateTime
- DateTimeOffset
- enums
- nullable enums
- records
- classes
- interfaces
- arbitrary reference types

The generator should not artificially restrict property types unless the underlying implementation requires it.

---

# Type Semantics

Provider values are stored as objects.

For:

```csharp
string? UserUid
```

a generated getter should effectively perform:

```csharp
return AmbientContext.Get<string>("UserUid");
```

For:

```csharp
int Count
```

a missing value should return:

```csharp
default(int)
```

unless future functionality introduces required-value semantics.

---

# Missing Activity Behavior

If the default Activity provider is active but:

```csharp
Activity.Current == null
```

then:

- Reads return default values
- Writes are ignored
- Contains returns false
- Remove returns false

The default behavior should not throw solely because an Activity is absent.

This allows library code to function safely outside ASP.NET Core.

---

# Optional Strict Mode

A future strict mode may allow applications to request failure when no ambient scope exists.

For example:

```csharp
AmbientContextOptions.RequireActiveContext = true;
```

This is not required for version 1.

---

# EF Core Interceptor Usage

A principal use case is EF Core interception.

Example:

```csharp
public override InterceptionResult<int> SavingChanges(
    DbContextEventData eventData,
    InterceptionResult<int> result)
{
    var audit =
        Ambient<IAuditContext>.Current;

    foreach (var entry in eventData.Context!.ChangeTracker.Entries())
    {
        if (entry.Entity is IAuditable entity)
        {
            entity.ModifiedBy = audit.UserUid;
        }
    }

    return result;
}
```

The interceptor:

- does not require `IHttpContextAccessor`
- does not require a scoped service
- does not know Activity is being used
- remains simple to test

---

# ASP.NET Core Middleware Usage

Middleware establishes ambient values:

```csharp
var user =
    Ambient<IUserContext>.Current;

user.UserUid =
    authenticatedUser.Uid;

user.UserDescription =
    authenticatedUser.Description;

Ambient<ITenantContext>.Current.Tenant =
    currentTenant.Name;

await next(context);
```

Every downstream component executing within the same Activity can read those values.

---

# Read-Only Views

Interfaces may expose read-only properties.

Example:

```csharp
[AmbientContext]
public interface IAuditContext
{
    string? UserUid { get; }

    string? UserDescription { get; }

    string? Tenant { get; }
}
```

Generated implementations should honor the declared interface contract.

A consumer of `IAuditContext` cannot mutate values even though another interface may expose setters for the same underlying keys.

---

# AOT Requirements

Pondhawk.Context must be designed for Native AOT from the beginning.

The runtime execution path must not depend on:

- `DispatchProxy`
- Castle DynamicProxy
- `Reflection.Emit`
- runtime code generation
- runtime expression compilation
- dynamic assembly generation
- late discovery of interface members

The source generator is responsible for discovering and implementing context interfaces at compile time.

---

# Trimming Requirements

The package should be trimming friendly.

Generated implementations should directly reference:

- interface types
- property types
- runtime APIs

This minimizes reliance on reflection metadata preserved solely for runtime discovery.

The package should build cleanly with trimming analysis enabled.

---

# AOT Verification

The solution should include a small test application that publishes with Native AOT.

Suggested command:

```bash
dotnet publish -c Release -r linux-x64 -p:PublishAot=true
```

The test should verify:

```csharp
Ambient<IUserContext>.Current.UserUid = "test";

Debug.Assert(
    Ambient<IUserContext>.Current.UserUid == "test");
```

AOT compatibility should be part of CI.

---

# Source Generator Technology

The generator should be implemented as a Roslyn Incremental Source Generator.

Use:

```text
IIncrementalGenerator
```

rather than the older:

```text
ISourceGenerator
```

model.

The generator should:

1. Identify `[AmbientContext]` interfaces.
2. Validate their members.
3. Generate concrete implementations.
4. Generate interface-to-implementation resolution.
5. Emit useful compiler diagnostics.
6. Avoid unnecessary regeneration.
7. Produce deterministic source.

---

# Generated Source Quality

Generated source should:

- Be readable
- Include `GeneratedCodeAttribute`
- Use deterministic naming
- Avoid unnecessary allocations
- Avoid reflection
- Avoid dynamic
- Compile under nullable reference types
- Work with analyzers enabled

Example header:

```csharp
[GeneratedCode(
    "Pondhawk.Context.Generator",
    "1.0.0")]
```

---

# Performance

A generated property read should conceptually be:

```text
Generated property getter
        ↓
AmbientContext.Get<T>
        ↓
Current provider
        ↓
Activity.Current
        ↓
GetCustomProperty
        ↓
Cast
```

No reflection occurs during normal property access.

No proxy objects are created per request.

No dictionaries are copied.

No serialization occurs.

---

# Allocation Requirements

After startup and static initialization:

```text
Ambient<IUserContext>.Current.UserUid
```

should not allocate solely because of Pondhawk.Context.

Reference-type values naturally retain their existing object references.

Value types may require boxing at the provider boundary because Activity custom properties store `object`.

This is acceptable for version 1.

---

# Concurrency

Generated proxy objects are stateless and shared globally.

Request isolation is provided by the active provider.

The Activity provider relies on:

```csharp
Activity.Current
```

which flows with the current execution context.

The generated implementation must not store request-specific data in:

- static fields
- instance fields
- singleton state

---

# Testing

## Runtime Tests

Test:

- Set/Get
- Missing values
- Nullable values
- Value types
- Reference types
- Activity isolation
- Async flow
- Concurrent requests
- Interface overlap
- Read-only views
- Interface inheritance

---

## Generator Tests

Use Roslyn generator tests to verify:

- Correct generated source
- Diagnostics
- Unsupported methods
- Unsupported events
- Indexers
- Inheritance
- Duplicate names
- Nullable annotations

---

## Native AOT Tests

At least one CI test application should publish and execute under Native AOT.

---

# Test Provider

A test provider should allow tests to execute without creating an Activity.

Example concept:

```csharp
using var scope =
    AmbientContext.UseProvider(
        new TestAmbientContextProvider());

Ambient<IUserContext>.Current.UserUid =
    "test-user";
```

Care must be taken that provider overrides themselves do not create unsafe global mutable state.

A scoped provider override may use an appropriate execution-local mechanism if required specifically for testing.

---

# Optional Scoped Overrides

Future versions may support:

```csharp
using (AmbientContext.Override<IUserContext>(
    x => x.UserUid = "system"))
{
    ...
}
```

The original value would be restored when the scope is disposed.

This is useful for:

- System operations
- Impersonation
- Tests
- Nested jobs

Not required for version 1.

---

# Context Snapshots

A future version may support capturing a set of ambient values:

```csharp
var snapshot =
    AmbientContext.Capture();
```

and restoring them later:

```csharp
using (snapshot.Apply())
{
    ...
}
```

This may be useful when explicitly moving work outside the original Activity.

This functionality should not be added until a concrete use case exists.

---

# Non-Goals

Pondhawk.Context does not replace:

- Dependency Injection
- Authentication
- Authorization
- OpenTelemetry
- Logging
- Configuration
- Distributed tracing
- Messaging context propagation

It provides ambient execution context only.

---

# Distributed Propagation

Pondhawk.Context values must not automatically flow across process boundaries.

The Activity provider specifically uses:

```text
Activity.CustomProperties
```

rather than:

```text
Activity.Baggage
```

This is intentional.

Values such as:

```text
Current User Description
Current Tenant
Internal Audit Metadata
```

must not automatically become distributed request headers.

If distributed propagation is ever required, it should be implemented explicitly by a separate component.

---

# Security

Ambient context must not be treated as an authorization boundary.

For example:

```csharp
Ambient<IUserContext>.Current.UserUid
```

may describe the current user, but authorization decisions should still rely on the application's trusted identity and authorization mechanisms.

Context values are ambient application metadata.

---

# Example End-to-End

Define interfaces:

```csharp
[AmbientContext]
public interface IUserContext
{
    string? UserUid { get; set; }

    string? UserDescription { get; set; }
}

[AmbientContext]
public interface ITenantContext
{
    string? Tenant { get; set; }
}

[AmbientContext]
public interface IAuditContext
{
    string? UserUid { get; }

    string? UserDescription { get; }

    string? Tenant { get; }
}
```

ASP.NET Core middleware:

```csharp
Ambient<IUserContext>.Current.UserUid =
    principal.GetUserUid();

Ambient<IUserContext>.Current.UserDescription =
    principal.GetDisplayName();

Ambient<ITenantContext>.Current.Tenant =
    tenant.Name;

await next(context);
```

EF Core interceptor:

```csharp
var audit =
    Ambient<IAuditContext>.Current;

entity.ModifiedBy =
    audit.UserUid;

entity.ModifiedByDescription =
    audit.UserDescription;

entity.ModifiedTenant =
    audit.Tenant;
```

No consumer needs to know that the values are stored in Activity.

---

# Project Layout

Suggested solution structure:

```text
Pondhawk.Context.sln

src/
    Pondhawk.Context/
        Ambient.cs
        AmbientContext.cs
        IAmbientContextProvider.cs
        ActivityAmbientContextProvider.cs
        AmbientContextAttribute.cs

    Pondhawk.Context.Generator/
        AmbientContextGenerator.cs
        Diagnostics/
        Models/
        Emitters/

tests/
    Pondhawk.Context.Tests/
    Pondhawk.Context.Generator.Tests/
    Pondhawk.Context.AotTests/
```

---

# NuGet Packaging

Consumers should install:

```text
Pondhawk.Context
```

The package should include the generator automatically.

The generator assembly should be exposed as an analyzer asset and should not become a runtime dependency of applications.

---

# Version 1 Scope

Version 1 should include:

- `Ambient<T>.Current`
- `[AmbientContext]`
- Compile-time generated implementations
- Activity provider
- Provider abstraction
- Basic dictionary-style API
- Read/write properties
- Read-only properties
- Interface inheritance where unambiguous
- Compiler diagnostics
- Unit tests
- Generator tests
- Native AOT test application
- NuGet packaging
- XML documentation
- README documentation

---

# Explicit Version 1 Exclusions

Do not implement yet:

- DispatchProxy fallback
- DynamicProxy fallback
- Runtime reflection fallback
- Distributed propagation
- Property alias attributes
- Scoped overrides
- Context snapshots
- Automatic logging enrichment
- OpenTelemetry integration
- HttpContext-specific provider
- DI-specific provider

These may be evaluated later if concrete requirements emerge.

---

# Critical Implementation Rule

The implementation must preserve this programming model:

```csharp
Ambient<IUserContext>.Current.UserUid
```

while ensuring the implementation behind it is statically generated and Native AOT compatible.

The consumer should get the convenience of a dynamic-style interface projection without any dynamic runtime machinery.

---

# Final Recommendation

Pondhawk.Context should be implemented as a standalone, source-generator-first NuGet package.

Its foundational architecture is:

```text
Strongly Typed Interface
        +
Compile-Time Generated Implementation
        +
Provider Abstraction
        +
Activity.Current.CustomProperties
```

The framework should remain intentionally small.

Its purpose is singular:

> **Provide strongly typed, async-flowing ambient application context without coupling consumers to the underlying storage mechanism, while remaining fully compatible with trimming and Native AOT.**