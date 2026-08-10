using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Pondhawk.Context.Generator.Tests;

/// <summary>
/// Compiles a snippet with the ambient context generator attached and reports what came
/// out: the diagnostics and the generated source.
/// </summary>
internal static class GeneratorHarness
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    public static GeneratorRunResult Run(string source, LanguageVersion languageVersion = LanguageVersion.CSharp11)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(
            source,
            new CSharpParseOptions(languageVersion));

        var compilation = CSharpCompilation.Create(
            "GeneratorTests",
            [syntaxTree],
            References,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver
            .Create(new AmbientContextGenerator())
            .WithUpdatedParseOptions(new CSharpParseOptions(languageVersion))
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var result = driver.GetRunResult().Results.Single();

        return new GeneratorRunResult(
            result.Diagnostics,
            [.. result.GeneratedSources.Select(generated => generated.SourceText.ToString())],
            output.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToImmutableArray());
    }

    private static ImmutableArray<MetadataReference> BuildReferences() =>
    [
        .. AppDomain.CurrentDomain
            .GetAssemblies()
            .Where(assembly => !assembly.IsDynamic && !string.IsNullOrWhiteSpace(assembly.Location))
            .Select(assembly => (MetadataReference)MetadataReference.CreateFromFile(assembly.Location)),
        MetadataReference.CreateFromFile(typeof(AmbientContextAttribute).Assembly.Location),
        MetadataReference.CreateFromFile(typeof(Assembly).Assembly.Location),
    ];
}

/// <param name="Diagnostics">Diagnostics reported by the generator itself.</param>
/// <param name="GeneratedSources">Generated source text, in generation order.</param>
/// <param name="CompilationErrors">
/// Errors from compiling the original source together with the generated output. Any
/// entry here means the generator emitted code that does not compile.
/// </param>
internal sealed record GeneratorRunResult(
    ImmutableArray<Diagnostic> Diagnostics,
    ImmutableArray<string> GeneratedSources,
    ImmutableArray<Diagnostic> CompilationErrors)
{
    public bool HasDiagnostic(string id) =>
        Diagnostics.Any(diagnostic => string.Equals(diagnostic.Id, id, StringComparison.Ordinal));
}
