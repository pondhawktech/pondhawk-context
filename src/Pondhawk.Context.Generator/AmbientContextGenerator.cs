using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Pondhawk.Context.Generator.Diagnostics;
using Pondhawk.Context.Generator.Emitters;
using Pondhawk.Context.Generator.Models;

namespace Pondhawk.Context.Generator;

/// <summary>
/// Generates implementations for interfaces marked with <c>[AmbientContext]</c>.
/// </summary>
/// <remarks>
/// For each such interface the generator emits a second partial declaration that
/// implements <c>IAmbientContextInstance&lt;TSelf&gt;</c>, plus a stateless singleton
/// whose accessors route to <c>AmbientContext</c>. Resolution is therefore entirely
/// compile-time: no reflection, no registry, no startup hook.
/// </remarks>
[Generator(LanguageNames.CSharp)]
public sealed class AmbientContextGenerator : IIncrementalGenerator
{
    private const string AttributeMetadataName = "Pondhawk.Context.AmbientContextAttribute";

    private static readonly SymbolDisplayFormat TypeFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.UseSpecialTypes
            | SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <inheritdoc />
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeMetadataName,
                predicate: static (node, _) => node is InterfaceDeclarationSyntax or TypeDeclarationSyntax,
                transform: static (ctx, ct) => Parse(ctx, ct))
            .Where(static model => model is not null)
            .Select(static (model, _) => model!);

        var languageVersion = context.CompilationProvider.Select(static (compilation, _) =>
            compilation is CSharpCompilation csharp ? csharp.LanguageVersion : LanguageVersion.Latest);

        context.RegisterSourceOutput(
            models.Combine(languageVersion),
            static (spc, pair) => Execute(spc, pair.Left, pair.Right));
    }

    private static void Execute(SourceProductionContext context, AmbientContextModel model, LanguageVersion language)
    {
        foreach (var diagnostic in model.Diagnostics)
            context.ReportDiagnostic(diagnostic.ToDiagnostic());

        // The generated interface part uses static abstract member implementation,
        // which is C# 11. Without this check the consumer sees errors in generated files.
        if (language < LanguageVersion.CSharp11)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.LanguageVersionTooLow,
                location: null,
                model.InterfaceName));
            return;
        }

        if (!model.CanEmit)
            return;

        context.AddSource(model.HintName, AmbientContextEmitter.Emit(model));
    }

    private static AmbientContextModel? Parse(GeneratorAttributeSyntaxContext context, CancellationToken cancellationToken)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol)
            return null;

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        var location = LocationInfo.From(symbol);
        var name = symbol.Name;

        if (symbol.TypeKind != TypeKind.Interface)
        {
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.MustBeInterface, location, name));
            return Invalid(symbol, diagnostics.ToImmutable());
        }

        if (symbol.ContainingType is not null)
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.NestedNotSupported, location, name));

        if (symbol.Arity > 0)
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.GenericNotSupported, location, name));

        if (!IsPartial(symbol, cancellationToken))
            diagnostics.Add(new DiagnosticInfo(DiagnosticDescriptors.MustBePartial, location, name));

        var properties = CollectProperties(symbol, diagnostics, cancellationToken);

        var containingNamespace = symbol.ContainingNamespace.IsGlobalNamespace
            ? null
            : symbol.ContainingNamespace.ToDisplayString();

        var implementationName = BuildImplementationName(name);
        var hintName = containingNamespace is null
            ? $"{name}.g.cs"
            : $"{containingNamespace}.{name}.g.cs";

        return new AmbientContextModel(
            containingNamespace,
            name,
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            SyntaxFacts.GetText(symbol.DeclaredAccessibility),
            implementationName,
            hintName,
            properties,
            diagnostics.ToImmutable());
    }

    /// <summary>
    /// Walks the interface and everything it inherits, validating members and reducing
    /// the properties to a single set keyed by name.
    /// </summary>
    private static ImmutableArray<AmbientPropertyModel> CollectProperties(
        INamedTypeSymbol symbol,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        CancellationToken cancellationToken)
    {
        var byName = new Dictionary<string, AmbientPropertyModel>(System.StringComparer.Ordinal);
        var ambiguous = new HashSet<string>(System.StringComparer.Ordinal);
        var ordered = ImmutableArray.CreateBuilder<AmbientPropertyModel>();

        var interfaces = new List<INamedTypeSymbol> { symbol };
        interfaces.AddRange(symbol.AllInterfaces);

        foreach (var declaring in interfaces)
        {
            foreach (var member in declaring.GetMembers())
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Static members and default implementations are the interface's own
                // business — the generated class does not need to provide them.
                if (member.IsStatic)
                    continue;

                var memberLocation = LocationInfo.From(member);

                switch (member)
                {
                    case IEventSymbol:
                        diagnostics.Add(new DiagnosticInfo(
                            DiagnosticDescriptors.EventsNotSupported, memberLocation, symbol.Name, member.Name));
                        break;

                    case IMethodSymbol method when !IsAccessor(method):
                        if (method.IsAbstract)
                        {
                            diagnostics.Add(new DiagnosticInfo(
                                DiagnosticDescriptors.PropertiesOnly, memberLocation, symbol.Name, method.Name));
                        }

                        break;

                    case IPropertySymbol { IsIndexer: true }:
                        diagnostics.Add(new DiagnosticInfo(
                            DiagnosticDescriptors.IndexersNotSupported, memberLocation, symbol.Name));
                        break;

                    case IPropertySymbol property when property.IsAbstract:
                        AddProperty(property, memberLocation, symbol, diagnostics, byName, ambiguous, ordered);
                        break;
                }
            }
        }

        return ordered.Where(p => !ambiguous.Contains(p.Name)).ToImmutableArray();
    }

    private static void AddProperty(
        IPropertySymbol property,
        LocationInfo? location,
        INamedTypeSymbol owner,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        Dictionary<string, AmbientPropertyModel> byName,
        HashSet<string> ambiguous,
        ImmutableArray<AmbientPropertyModel>.Builder ordered)
    {
        if (property.SetMethod is { IsInitOnly: true })
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.InitOnlyNotSupported, location, owner.Name, property.Name));
            return;
        }

        var isNonNullableReference =
            property.Type.IsReferenceType
            && property.NullableAnnotation == NullableAnnotation.NotAnnotated;

        if (isNonNullableReference)
        {
            diagnostics.Add(new DiagnosticInfo(
                DiagnosticDescriptors.NonNullableReferenceProperty, location, owner.Name, property.Name));
        }

        var model = new AmbientPropertyModel(
            property.Name,
            property.Type.ToDisplayString(TypeFormat),
            property.Type.WithNullableAnnotation(NullableAnnotation.NotAnnotated).ToDisplayString(TypeFormat),
            property.GetMethod is not null,
            property.SetMethod is not null,
            isNonNullableReference);

        if (byName.TryGetValue(property.Name, out var existing))
        {
            // Identical re-declarations collapse; anything else is a real conflict the
            // generated class could not satisfy.
            if (!existing.IsCompatibleWith(model) && ambiguous.Add(property.Name))
            {
                diagnostics.Add(new DiagnosticInfo(
                    DiagnosticDescriptors.AmbiguousProperty, location, owner.Name, property.Name));
            }

            return;
        }

        byName.Add(property.Name, model);
        ordered.Add(model);
    }

    private static bool IsAccessor(IMethodSymbol method) =>
        method.MethodKind is MethodKind.PropertyGet
            or MethodKind.PropertySet
            or MethodKind.EventAdd
            or MethodKind.EventRemove
            or MethodKind.EventRaise;

    private static bool IsPartial(INamedTypeSymbol symbol, CancellationToken cancellationToken) =>
        symbol.DeclaringSyntaxReferences
            .Select(reference => reference.GetSyntax(cancellationToken))
            .OfType<TypeDeclarationSyntax>()
            .Any(declaration => declaration.Modifiers.Any(SyntaxKind.PartialKeyword));

    private static string BuildImplementationName(string interfaceName)
    {
        var stem = interfaceName.Length > 1 && interfaceName[0] == 'I' && char.IsUpper(interfaceName[1])
            ? interfaceName.Substring(1)
            : interfaceName;

        return stem + "AmbientImplementation";
    }

    private static AmbientContextModel Invalid(INamedTypeSymbol symbol, ImmutableArray<DiagnosticInfo> diagnostics) =>
        new(
            symbol.ContainingNamespace.IsGlobalNamespace ? null : symbol.ContainingNamespace.ToDisplayString(),
            symbol.Name,
            symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
            SyntaxFacts.GetText(symbol.DeclaredAccessibility),
            BuildImplementationName(symbol.Name),
            $"{symbol.Name}.g.cs",
            ImmutableArray<AmbientPropertyModel>.Empty,
            diagnostics);
}
