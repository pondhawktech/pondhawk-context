#nullable enable

using System.Text.Json;
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
    public string BuildNumber { get; }
    public string? VersionSuffix { get; }
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
        BuildNumber = context.Argument("build-number",
            context.Environment.GetEnvironmentVariable("BUILD_NUMBER") ?? "0");
        VersionSuffix = context.Argument<string?>("version-suffix", null)
            ?? context.Environment.GetEnvironmentVariable("VERSION_SUFFIX");
        if (BuildNumber == "0" && VersionSuffix is null)
            VersionSuffix = "local";
        Artifacts = context.Argument("artifacts", "artifacts");
        NuGetSource = context.Argument<string?>("nuget-source", null)
            ?? context.Environment.GetEnvironmentVariable("NUGET_SOURCE");
        NuGetApiKey = context.Argument<string?>("nuget-api-key", null)
            ?? context.Environment.GetEnvironmentVariable("NUGET_API_KEY");
        AotRuntime = context.Argument("aot-runtime", "linux-x64");
    }

    /// <summary>
    /// Major and minor come from the project's version.json; the patch is the CI build
    /// number, so nothing has to be bumped by hand between releases.
    /// </summary>
    public string GetProjectVersion(string projectPath)
    {
        var projectDir = System.IO.Path.GetDirectoryName(projectPath)!;
        var versionFile = System.IO.Path.Combine(projectDir, "version.json");
        var json = System.IO.File.ReadAllText(versionFile);
        var doc = JsonDocument.Parse(json);
        var majorMinor = doc.RootElement.GetProperty("version").GetString()
            ?? throw new System.InvalidOperationException($"Missing 'version' in {versionFile}");
        var version = $"{majorMinor}.{BuildNumber}";
        if (VersionSuffix is not null)
            version = $"{version}-{VersionSuffix}";
        return version;
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
            var version = context.GetProjectVersion(project);
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
                SkipDuplicate = true
            });
        }
    }
}

[TaskName("Default")]
[IsDependentOn(typeof(PushTask))]
public sealed class DefaultTask : FrostingTask<BuildContext>;
