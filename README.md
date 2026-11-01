<p align="center">
  <img src="pht-small-logo.png" alt="Pondhawk.Context" width="120" />
</p>

<h1 align="center">Pondhawk.Context</h1>

<p align="center">
  Strongly typed, async-flowing ambient application context for .NET — compile-time generated, with no dependency injection, no runtime reflection, and no proxies.
</p>

<p align="center">
  <a href="https://github.com/pondhawktech/pondhawk-context/actions/workflows/build.yml"><img src="https://github.com/pondhawktech/pondhawk-context/actions/workflows/build.yml/badge.svg" alt="Build" /></a>
  <img src="https://img.shields.io/badge/.NET-8.0%20%7C%2010.0-512bd4" alt=".NET 8 and .NET 10" />
  <img src="https://img.shields.io/badge/license-MIT-blue" alt="MIT License" />
  <a href="https://www.nuget.org/packages/Pondhawk.Context"><img src="https://img.shields.io/nuget/v/Pondhawk.Context?label=NuGet" alt="Pondhawk.Context on NuGet" /></a>
</p>

Implemented with a Roslyn incremental source generator, and fully compatible with
trimming and Native AOT.

```csharp
[AmbientContext]
public partial interface IUserContext
{
    string? UserUid { get; set; }
}

Ambient<IUserContext>.Current.UserUid = "alice";
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
dotnet run --project build/Build.csproj -- --target=Pack
dotnet run --project build/Build.csproj -- --target=AotVerify     # needs clang + zlib1g-dev
```

Targets are `Clean → Restore → Build → Test → Pack → Push`, with `AotVerify` standing
alone so pull requests stay fast.

## Versioning

`<VersionPrefix>` in `src/Directory.Build.props` holds the last released version.

```text
Released version        1.1.8
Push to main            1.1.9-ci.42    (GitHub Packages only)
Local Pack              1.1.8-local
Release, bump: patch    1.1.9
```

`publish.yml` is a manual workflow. Choose `bump` (`none`, `patch`, `minor` or `major`). It bumps the
version, then builds, tests, and packs. It pushes to nuget.org and GitHub Packages, commits the bump,
and creates the tag and GitHub release. `dry_run` stops before it publishes, and can run on any branch.
A push to nuget.org is permanent: a version can be unlisted but never replaced.

## Requirements

- .NET 8 SDK or later to build; the runtime library targets `net8.0` and `net10.0`
- Consumers need C# 11 or later — the generated code uses static abstract interface members
- `clang` and `zlib1g-dev` for the Native AOT verification target

MIT licensed. © Pond Hawk Technologies Inc.
