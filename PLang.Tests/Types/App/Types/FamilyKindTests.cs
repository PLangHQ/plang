namespace PLang.Tests.App.Types
{
    /// <summary>
    /// A family that declares it has kinds (<c>[Kinds]</c>) holds each class under it as one of its kinds, by the
    /// class's own name, and a value of such a class reads as <c>{family, kind}</c> — so <c>is mouse</c> answers with no
    /// kind class written beside it. A kind the family already holds (path's schemes) is not held twice.
    /// </summary>
    public class FamilyKindTests
    {
        private global::app.actor.context.@this Ctx(global::app.@this app) => app.actor.list.User.Context;

        [Test]
        public async Task AFamilysSubclass_IsOneOfItsKinds_AndItsValueReadsAsIt()
        {
            await using var app = new global::app.@this("/app").Testing();
            var types = new global::app.type.list.@this();
            await types.Add(typeof(Gadget.Gadget).Assembly, Ctx(app)).IsSuccess();
            var mouse = new Gadget.Mouse();

            await Assert.That(types["gadget"].kind["mouse"]).IsNotNull();
            await Assert.That(mouse.Type.Name).IsEqualTo("gadget");
            await Assert.That(mouse.Type.kind.Name).IsEqualTo("mouse");
            await Assert.That(mouse.Is("mouse", types)).IsTrue();
            await Assert.That(mouse.Is("gadget", types)).IsTrue();
        }

        // query's clauses are its kinds now; path holds file and http once, as its schemes
        [Test]
        public async Task QuerysClausesAreItsKinds_PathsSchemesAreNotHeldTwice()
        {
            await using var app = new global::app.@this("/app").Testing();

            await Assert.That(app.type.list["query"].kind["where"]).IsNotNull();
            await Assert.That(app.type.list["path"].kind["file"]).IsTypeOf<global::app.type.item.path.scheme.@this>();
            await Assert.That(app.type.list["path"].kind["http"]).IsTypeOf<global::app.type.item.path.scheme.@this>();
        }
    }
}

namespace PLang.Tests.App.Types.Gadget
{
    /// <summary>A test-only family with kinds: each class under it is one.</summary>
    [global::app.Attributes.PlangType("gadget"), global::app.Attributes.Kinds]
    public class Gadget : global::app.type.item.@this, global::app.type.item.ICreate<Gadget>
    {
        public static Gadget? Create(object? raw, global::app.type.@this? declared, global::app.data.@this data) => raw as Gadget;
    }

    /// <summary>A gadget's kind, by its name.</summary>
    public sealed class Mouse : Gadget
    {
    }
}
