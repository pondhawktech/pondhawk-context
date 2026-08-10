# Pondhawk.Context

Strongly typed, async-flowing ambient application context for .NET, implemented with a
Roslyn incremental source generator. No dependency injection, no runtime reflection, no
proxies — and fully compatible with trimming and Native AOT.

```csharp
[AmbientContext]
public partial interface IUserContext
{
    string? UserUid { get; set; }
}

Ambient<IUserContext>.Current.UserUid = "jmoring";
```

For usage, see the [package README](src/Pondhawk.Context/README.md). For the design
specification, see [Pondhawk.Context.md](Pondhawk.Context.md); for the decisions that
supersede parts of it, see [DECISIONS.md](DECISIONS.md).

## Layout

```text
build/                                  Cake Frosting build
src/Pondhawk.Context/                   Runtime library (net8.0; net10.0)
src/Pondhawk.Context.Generator/         Incremental source generator (netstandard2.0)
test/Pondhawk.Context.Tests/            Runtime tests, both TFMs
test/Pondhawk.Context.Generator.Tests/  Generator output and diagnostic tests
test/Pondhawk.Context.AotTests/         Published natively and executed in CI
```

The generator is not published as its own package. It is built first and injected into
`Pondhawk.Context.nupkg` under `analyzers/dotnet/cs`, so consumers get it from a single
`PackageReference` and it never becomes a runtime dependency.

## Building

```bash
dotnet run --project build/Build.csproj -- --target=Test
dotnet run --project build/Build.csproj -- --target=Pack --build-number=123
dotnet run --project build/Build.csproj -- --target=AotVerify     # needs clang + zlib1g-dev
```

Targets are `Clean → Restore → Build → Test → Pack → Push`, with `AotVerify` standing
alone so pull requests stay fast.

## Versioning

`src/Pondhawk.Context/version.json` holds the major and minor version. The patch comes
from the CI build number, so nothing needs bumping by hand between releases:

```text
version.json 1.0  +  run number 42   ->  1.0.42
version.json 1.0  +  local build     ->  1.0.0-local
```

`build.yml` publishes to GitHub Packages on every push to `main`. `publish.yml` is a
manual workflow that promotes an existing build's artifacts to nuget.org.

## Requirements

- .NET 8 SDK or later to build; the runtime library targets `net8.0` and `net10.0`
- Consumers need C# 11 or later — the generated code uses static abstract interface members
- `clang` and `zlib1g-dev` for the Native AOT verification target

MIT licensed. © Pond Hawk Technologies Inc.
