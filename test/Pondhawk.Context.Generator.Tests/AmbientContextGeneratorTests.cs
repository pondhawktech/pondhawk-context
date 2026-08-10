using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using static VerifyXunit.Verifier;

namespace Pondhawk.Context.Generator.Tests;

public sealed class AmbientContextGeneratorTests
{
    [Fact]
    public Task GeneratesImplementationAndInterfacePart()
    {
        var result = GeneratorHarness.Run("""
            using Pondhawk.Context;

            namespace Sample;

            [AmbientContext]
            public partial interface IUserContext
            {
                string? UserUid { get; set; }
                string? UserDescription { get; }
                int RequestCount { get; set; }
                System.Guid? CorrelationId { get; set; }
            }
            """);

        result.CompilationErrors.ShouldBeEmpty();
        return Verify(result.GeneratedSources.Single());
    }

    [Fact]
    public void GeneratedCode_Compiles()
    {
        var result = GeneratorHarness.Run("""
            using Pondhawk.Context;

            namespace Sample;

            [AmbientContext]
            public partial interface ITenantContext
            {
                string? Tenant { get; set; }
            }
            """);

        result.CompilationErrors.ShouldBeEmpty();
        result.Diagnostics.ShouldBeEmpty();
    }

    [Fact]
    public void NonPartialInterface_ReportsPhctx007()
    {
        var result = GeneratorHarness.Run("""
            using Pondhawk.Context;

            [AmbientContext]
            public interface IUserContext
            {
                string? UserUid { get; set; }
            }
            """);

        result.HasDiagnostic("PHCTX007").ShouldBeTrue();
        result.GeneratedSources.ShouldBeEmpty();
    }

    [Fact]
    public void Method_ReportsPhctx001()
    {
        var result = GeneratorHarness.Run("""
            using Pondhawk.Context;

            [AmbientContext]
            public partial interface IUserContext
            {
                string? UserUid { get; set; }
                void Save();
            }
            """);

        result.HasDiagnostic("PHCTX001").ShouldBeTrue();
    }

    [Fact]
    public void Event_ReportsPhctx004()
    {
        var result = GeneratorHarness.Run("""
            using Pondhawk.Context;

            [AmbientContext]
            public partial interface IUserContext
            {
                event System.EventHandler Changed;
            }
            """);

        result.HasDiagnostic("PHCTX004").ShouldBeTrue();
    }

    [Fact]
    public void Indexer_ReportsPhctx003()
    {
        var result = GeneratorHarness.Run("""
            using Pondhawk.Context;

            [AmbientContext]
            public partial interface IUserContext
            {
                string this[int index] { get; }
            }
            """);

        result.HasDiagnostic("PHCTX003").ShouldBeTrue();
    }

    [Fact]
    public void GenericInterface_ReportsPhctx005()
    {
        var result = GeneratorHarness.Run("""
            using Pondhawk.Context;

            [AmbientContext]
            public partial interface IUserContext<T>
            {
                T? Value { get; set; }
            }
            """);

        result.HasDiagnostic("PHCTX005").ShouldBeTrue();
    }

    [Fact]
    public void InitOnlySetter_ReportsPhctx008()
    {
        var result = GeneratorHarness.Run("""
            using Pondhawk.Context;

            [AmbientContext]
            public partial interface IUserContext
            {
                string? UserUid { get; init; }
            }
            """);

        result.HasDiagnostic("PHCTX008").ShouldBeTrue();
    }

    [Fact]
    public void NonNullableReferenceProperty_WarnsPhctx009ButStillGenerates()
    {
        var result = GeneratorHarness.Run("""
            using Pondhawk.Context;

            [AmbientContext]
            public partial interface IUserContext
            {
                string UserUid { get; set; }
            }
            """);

        result.HasDiagnostic("PHCTX009").ShouldBeTrue();
        result.GeneratedSources.ShouldNotBeEmpty();
        result.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void InheritedProperties_AreImplemented()
    {
        var result = GeneratorHarness.Run("""
            using Pondhawk.Context;

            public interface IUserIdentity
            {
                string? UserUid { get; }
            }

            [AmbientContext]
            public partial interface IUserContext : IUserIdentity
            {
                string? UserDescription { get; set; }
            }
            """);

        result.Diagnostics.ShouldBeEmpty();
        result.GeneratedSources.Single().ShouldContain("UserUid");
        result.CompilationErrors.ShouldBeEmpty();
    }

    [Fact]
    public void ConflictingInheritedProperties_ReportPhctx006()
    {
        var result = GeneratorHarness.Run("""
            using Pondhawk.Context;

            public interface ILeft
            {
                string? Value { get; }
            }

            public interface IRight
            {
                int Value { get; }
            }

            [AmbientContext]
            public partial interface IUserContext : ILeft, IRight
            {
            }
            """);

        result.HasDiagnostic("PHCTX006").ShouldBeTrue();
    }

    [Fact]
    public void LanguageVersionBelow11_ReportsPhctx010()
    {
        var result = GeneratorHarness.Run(
            """
            using Pondhawk.Context;

            [AmbientContext]
            public partial interface IUserContext
            {
                string? UserUid { get; set; }
            }
            """,
            LanguageVersion.CSharp10);

        result.HasDiagnostic("PHCTX010").ShouldBeTrue();
        result.GeneratedSources.ShouldBeEmpty();
    }
}
