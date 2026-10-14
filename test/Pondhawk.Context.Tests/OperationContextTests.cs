using System.Diagnostics;
using Shouldly;

namespace Pondhawk.Context.Tests;

public sealed class OperationContextTests
{
    [Fact]
    public void Begin_StartsAUnitOfWorkForTheSystem()
    {
        using var operation = Operation.Begin();

        var context = Ambient<IOperationContext>.Current;

        Activity.Current.ShouldBeSameAs(operation);
        context.CorrelationId.ShouldNotBeNullOrEmpty();
        context.CorrelationId!.Length.ShouldBe(32);
        context.SubjectId.ShouldBe(Operation.System);
        context.SubjectName.ShouldBe(Operation.System);
        context.Tenant.ShouldBeNull();
    }

    [Fact]
    public void Begin_TakesTheCorrelationIdItIsGiven()
    {
        using var operation = Operation.Begin("01JABCDEFGHJKMNPQRSTVWXYZ0");

        Ambient<IOperationContext>.Current.CorrelationId.ShouldBe("01JABCDEFGHJKMNPQRSTVWXYZ0");
    }

    [Fact]
    public void Begin_GivesEachUnitOfWorkItsOwnCorrelationId()
    {
        string? first;

        using (Operation.Begin())
        {
            first = Ambient<IOperationContext>.Current.CorrelationId;
        }

        using var second = Operation.Begin();

        Ambient<IOperationContext>.Current.CorrelationId.ShouldNotBe(first);
    }

    [Fact]
    public void Disposed_TheUnitOfWorkEnds()
    {
        using (Operation.Begin())
        {
        }

        Activity.Current.ShouldBeNull();
        Ambient<IOperationContext>.Current.SubjectId.ShouldBeNull("with no activity there is no context");
    }

    [Fact]
    public void ARequestsValues_AreSeenInsideTheLibrariesItCalls()
    {
        using var request = new Activity("request").Start();

        var context = Ambient<IOperationContext>.Current;

        context.CorrelationId = "request-1";
        context.SubjectId = "01USER";
        context.SubjectName = "jmoring";
        context.Tenant = "acme";

        // A child activity, as EF Core or HttpClient would start one.
        using var child = new Activity("query").Start();

        context.SubjectId.ShouldBe("01USER");
        context.SubjectName.ShouldBe("jmoring");
        context.Tenant.ShouldBe("acme");
        context.CorrelationId.ShouldBe("request-1");
    }

    [Fact]
    public void ASubjectSetDeeper_ShadowsTheRequestsUntilItsActivityEnds()
    {
        using var request = new Activity("request").Start();

        var context = Ambient<IOperationContext>.Current;

        context.SubjectName = Operation.Anonymous;

        using (new Activity("sign-in").Start())
        {
            context.SubjectName = "jmoring";
            context.SubjectName.ShouldBe("jmoring");
        }

        context.SubjectName.ShouldBe(Operation.Anonymous);
    }

    [Fact]
    public void AnApplicationsOwnView_ReadsTheSameValues()
    {
        using var operation = Operation.Begin("view-1");

        Ambient<IOperationView>.Current.CorrelationId.ShouldBe("view-1");
        Ambient<IOperationView>.Current.SubjectId.ShouldBe(Operation.System);
    }

    [Fact]
    public void Values_NeverBecomeBaggage()
    {
        using var operation = Operation.Begin("private-1");

        Ambient<IOperationContext>.Current.SubjectName = "jmoring";

        operation.Baggage.ShouldBeEmpty("nothing ambient is sent on an outbound call");
    }
}
