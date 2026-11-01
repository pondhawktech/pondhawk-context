#nullable enable

using System.Linq;
using System.Text.RegularExpressions;
using Cake.Common;
using Cake.Common.IO;
using Cake.Common.Tools.DotNet;
using Cake.Common.Tools.DotNet.Build;
using Cake.Common.Tools.DotNet.Clean;
using Cake.Common.Tools.DotNet.MSBuild;
using Cake.Common.Tools.DotNet.NuGet.Push;
using Cake.Common.Tools.DotNet.Pack;
using Cake.Common.Tools.DotNet.Publish;
using Cake.Common.Tools.DotNet.Restore;
using Cake.Common.Tools.DotNet.Test;
using Cake.Core;
using Cake.Core.Diagnostics;
using Cake.Core.IO;
using Cake.Frosting;

return new CakeHost()
    .UseContext<BuildContext>()
    .Run(args);

public class BuildContext : FrostingContext
{
    public new string Configuration { get; }
    public string Solution { get; }
    public string? PackageVersion { get; }
    public bool SkipDuplicate { get; }
    public DirectoryPath Artifacts { get; }
    public string? NuGetSource { get; }
    public string? NuGetApiKey { get; }
    public string AotRuntime { get; }

    /// <summary>
    /// Only the runtime library is packed. The generator ships inside its package as an
    /// analyzer asset, so packing it separately would publish a second, useless package.
    /// </summary>
    public string[] SourceProjects { get; } =
    [
        "src/Pondhawk.Context/Pondhawk.Context.csproj"
    ];

    public string AotTestProject { get; } =
        "test/Pondhawk.Context.AotTests/Pondhawk.Context.AotTests.csproj";

    public BuildContext(ICakeContext context) : base(context)
    {
        Configuration = context.Argument("configuration", "Release");
        Solution = context.Argument("solution", "pondhawk-context.slnx");
        PackageVersion = context.Argument<string?>("package-version", null);
        SkipDuplicate = context.Argument("skip-duplicate", true);
        Artifacts = context.Argument("artifacts", "artifacts");
        NuGetSource = context.Argument<string?>("nuget-source", null)
            ?? context.Environment.GetEnvironmentVariable("NUGET_SOURCE");
        NuGetApiKey = context.Argument<string?>("nuget-api-key", null)
            ?? context.Environment.GetEnvironmentVariable("NUGET_API_KEY");
        AotRuntime = context.Argument("aot-runtime", "linux-x64");
    }

    private const string VersionFile = "src/Directory.Build.props";

    private static readonly Regex VersionPattern =
        new(@"<VersionPrefix>(?<version>\d+\.\d+\.\d+)</VersionPrefix>", RegexOptions.Compiled);

    /// <summary>The last released version, from <see cref="VersionFile"/>.</summary>
    public string ReadVersion()
    {
        var match = VersionPattern.Match(System.IO.File.ReadAllText(VersionFile));
        return match.Success
            ? match.Groups["version"].Value
            : throw new System.InvalidOperationException(
                $"No <VersionPrefix>MAJOR.MINOR.PATCH</VersionPrefix> in {VersionFile}.");
    }

    /// <summary>The next patch after the last released version. Does not change the file.</summary>
    public string NextPatch()
    {
        var parts = ReadVersion().Split('.').Select(int.Parse).ToArray();
        return $"{parts[0]}.{parts[1]}.{parts[2] + 1}";
    }

    /// <summary>Applies a semantic bump, rewrites <see cref="VersionFile"/>, and returns the new version.</summary>
    public string BumpVersion(string part)
    {
        var current = ReadVersion();
        var parts = current.Split('.').Select(int.Parse).ToArray();
        var next = part switch
        {
            "major" => $"{parts[0] + 1}.0.0",
            "minor" => $"{parts[0]}.{parts[1] + 1}.0",
            "patch" => $"{parts[0]}.{parts[1]}.{parts[2] + 1}",
            _ => throw new System.InvalidOperationException(
                $"Unknown bump '{part}' (expected none, major, minor or patch)."),
        };
        var text = System.IO.File.ReadAllText(VersionFile);
        System.IO.File.WriteAllText(VersionFile, VersionPattern.Replace(text, $"<VersionPrefix>{next}</VersionPrefix>"));
        return next;
    }
}

/// <summary>
/// Resolves a version and writes version=X.Y.Z to $GITHUB_OUTPUT under Actions.
/// --bump=patch rewrites the version file; --next=true --suffix=ci.42 leaves it unchanged.
/// </summary>
[TaskName("Version")]
public sealed class VersionTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var bump = context.Argument("bump", "none");
        var suffix = context.Argument("suffix", string.Empty);
        var version = bump != "none"
            ? context.BumpVersion(bump)
            : context.Argument("next", false) ? context.NextPatch() : context.ReadVersion();
        if (suffix.Length > 0)
            version = $"{version}-{suffix}";

        context.Log.Write(Verbosity.Normal, LogLevel.Information, "Version: {0}", version);
        var output = context.Environment.GetEnvironmentVariable("GITHUB_OUTPUT");
        if (!string.IsNullOrEmpty(output))
            System.IO.File.AppendAllText(output, $"version={version}\n");
    }
}

[TaskName("Clean")]
public sealed class CleanTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetClean(context.Solution, new DotNetCleanSettings
        {
            Configuration = context.Configuration
        });

        context.CleanDirectory(context.Artifacts);
    }
}

[TaskName("Restore")]
[IsDependentOn(typeof(CleanTask))]
public sealed class RestoreTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetRestore(context.Solution, new DotNetRestoreSettings());
    }
}

[TaskName("Build")]
[IsDependentOn(typeof(RestoreTask))]
public sealed class BuildTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetBuild(context.Solution, new DotNetBuildSettings
        {
            Configuration = context.Configuration,
            NoRestore = true
        });
    }
}

[TaskName("Test")]
[IsDependentOn(typeof(BuildTask))]
public sealed class TestTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        context.DotNetTest(context.Solution, new DotNetTestSettings
        {
            Configuration = context.Configuration,
            NoBuild = true,
            NoRestore = true
        });
    }
}

/// <summary>
/// Publishes the AOT test application natively and runs it. The trim and AOT analyzers
/// run on every ordinary build; this task is what proves the generated code still works
/// after the trimmer has been through it.
/// </summary>
[TaskName("AotVerify")]
public sealed class AotVerifyTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        var output = context.Artifacts.Combine("aot").Combine(context.AotRuntime);

        // PublishAot and TrimmerSingleWarn are set in the AOT test project itself, not
        // passed here: a command-line property is global and would flow into the
        // netstandard2.0 generator project, which cannot be AOT compiled (NETSDK1207).
        context.DotNetPublish(context.AotTestProject, new DotNetPublishSettings
        {
            Configuration = context.Configuration,
            Runtime = context.AotRuntime,
            OutputDirectory = output
        });

        var executable = output.CombineWithFilePath(
            context.IsRunningOnWindows()
                ? "Pondhawk.Context.AotTests.exe"
                : "Pondhawk.Context.AotTests");

        context.Log.Write(Verbosity.Normal, LogLevel.Information,
            "Running Native AOT verification: {0}", executable.FullPath);

        var exitCode = context.StartProcess(executable);
        if (exitCode != 0)
        {
            throw new System.InvalidOperationException(
                $"Native AOT verification failed with exit code {exitCode}.");
        }
    }
}

[TaskName("Pack")]
[IsDependentOn(typeof(TestTask))]
public sealed class PackTask : FrostingTask<BuildContext>
{
    public override void Run(BuildContext context)
    {
        foreach (var project in context.SourceProjects)
        {
            // A plain local Pack must never produce a version that looks released.
            var version = context.PackageVersion ?? $"{context.ReadVersion()}-local";
            context.Log.Write(Verbosity.Normal, LogLevel.Information,
                "Packing {0} with version {1}", project, version);
            context.DotNetPack(project, new DotNetPackSettings
            {
                Configuration = context.Configuration,
                NoBuild = true,
                NoRestore = true,
                OutputDirectory = context.Artifacts.FullPath,
                MSBuildSettings = new DotNetMSBuildSettings()
                    .WithProperty("PackageVersion", version)
                    .WithProperty("Version", version)
            });
        }
    }
}

[TaskName("Push")]
[IsDependentOn(typeof(PackTask))]
public sealed class PushTask : FrostingTask<BuildContext>
{
    public override bool ShouldRun(BuildContext context)
    {
        if (string.IsNullOrEmpty(context.NuGetSource) || string.IsNullOrEmpty(context.NuGetApiKey))
        {
            context.Log.Write(Verbosity.Normal, LogLevel.Warning,
                "Skipping Push — nuget-source or nuget-api-key not configured.");
            return false;
        }

        return true;
    }

    public override void Run(BuildContext context)
    {
        var packages = context.GetFiles($"{context.Artifacts.FullPath}/*.nupkg");

        foreach (var package in packages)
        {
            context.DotNetNuGetPush(package.FullPath, new DotNetNuGetPushSettings
            {
                Source = context.NuGetSource,
                ApiKey = context.NuGetApiKey,
                SkipDuplicate = context.SkipDuplicate
            });
        }
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(PushTask))]
public sealed class DefaultTask : FrostingTask<BuildContext>;
