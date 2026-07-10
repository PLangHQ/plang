using System.Linq;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// The clr→owning-entity routing distributes onto each family: number declares the
// numeric CLR types it owns, text declares string, path is reached by identity (its
// subclasses all resolve to the path entity). The central `if u == typeof(int) …`
// ladder is gone; routing composes from the family declarations and the entity index.
public class DistributedOwnerOfTests
{
    private static System.Type[] Clrs(System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> d)
        => d.Select(o => o.Clr).ToArray();

    private static global::app.type.list.@this Types => global::PLang.Tests.TestApp.SharedContext.App.Type;

    // The central switch is gone; routing composes from declarations. Pinned by
    // behaviour: the ownership door's answer for a CLR type is the owning family's
    // entity — and int is in number's own declaration. So extending ownership is an
    // edit to the family's declaration, never a central table.
    [Test] public async Task OwnerRouting_ComposesFromDeclarations_NotACentralSwitch()
    {
        await Assert.That(Types[typeof(int)]?.Name).IsEqualTo("number");
        await Assert.That(global::app.type.item.number.@this.OwnedClrTypes.Any(o => o.Clr == typeof(int))).IsTrue();
    }

    [Test] public async Task Number_DeclaresIntLongDecimalDoubleFloat()
    {
        var clrs = Clrs(global::app.type.item.number.@this.OwnedClrTypes);
        await Assert.That(clrs).Contains(typeof(int));
        await Assert.That(clrs).Contains(typeof(long));
        await Assert.That(clrs).Contains(typeof(decimal));
        await Assert.That(clrs).Contains(typeof(double));
        await Assert.That(clrs).Contains(typeof(float));
    }

    [Test] public async Task Text_DeclaresString()
    {
        await Assert.That(Clrs(global::app.type.item.text.@this.OwnedClrTypes)).Contains(typeof(string));
        await Assert.That(Types[typeof(string)]?.Name).IsEqualTo("text");
    }

    [Test] public async Task Path_ReachedByIdentity_EverySubclassResolvesToPath()
    {
        // path declares its base type Assignable — every scheme subclass routes to
        // the path entity through the entity index (identity + the name map).
        var pathDecl = global::app.type.item.path.@this.OwnedClrTypes;
        await Assert.That(pathDecl.Any(o => o.Assignable && o.Clr == typeof(global::app.type.item.path.@this))).IsTrue();
        await Assert.That(Types[typeof(global::app.type.item.path.file.@this)]?.Name).IsEqualTo("path");
    }

    // OwnerOf keys on the conversion *target*, so declaring byte[] would hijack every
    // byte[]-target conversion into image construction. image owns its own wrapper type;
    // raw bytes are decoded by image.Read, not by routing the byte[] CLR target to image.
    [Test] public async Task Image_DeclaresOwnWrapperType_NotByteArrayTarget()
    {
        await Assert.That(Clrs(global::app.type.item.image.@this.OwnedClrTypes))
            .Contains(typeof(global::app.type.item.image.@this));
        await Assert.That(Types[typeof(byte[])]?.Name).IsNotEqualTo("image");
    }

    [Test] public async Task OwnerRouting_PicksUpNewlyDeclaredClrType()
    {
        await Assert.That(Types[typeof(long)]?.Name).IsEqualTo("number");
        await Assert.That(Clrs(global::app.type.item.number.@this.OwnedClrTypes)).Contains(typeof(long));
    }
}
