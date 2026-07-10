using System.Linq;
using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.ReaderRegistryTests;

// The clr→family `OwnerOf` switch at app/type/convert/this.cs:58 distributes
// onto each family — number declares the numeric CLR types it owns, text
// declares string, path declares its subclasses. The central
// `if u == typeof(int) …` ladder dies; routing composes from the family
// declarations.
public class DistributedOwnerOfTests
{
    private static System.Type[] Clrs(System.Collections.Generic.IReadOnlyList<global::app.type.convert.OwnedClr> d)
        => d.Select(o => o.Clr).ToArray();

    // The central switch is gone; routing composes from declarations. Pinned
    // by behaviour: OwnerOf's answer for a CLR type is exactly what the owning
    // family declares — same family, same kind. So extending ownership is an
    // edit to the family's declaration, never to convert/this.cs.
    [Test] public async Task OwnerOf_CentralSwitch_NoLongerExists()
    {
        var (family, kind) = global::app.type.convert.@this.OwnerOf(typeof(int));
        await Assert.That(family).IsEqualTo(typeof(global::app.type.number.@this));
        await Assert.That(kind).IsEqualTo("int");
        // and the source of that answer is number's own declaration:
        var intDecl = global::app.type.number.@this.OwnedClrTypes.Single(o => o.Clr == typeof(int));
        await Assert.That(intDecl.Kind).IsEqualTo("int");
    }

    [Test] public async Task Number_DeclaresIntLongDecimalDoubleFloat()
    {
        var clrs = Clrs(global::app.type.number.@this.OwnedClrTypes);
        await Assert.That(clrs).Contains(typeof(int));
        await Assert.That(clrs).Contains(typeof(long));
        await Assert.That(clrs).Contains(typeof(decimal));
        await Assert.That(clrs).Contains(typeof(double));
        await Assert.That(clrs).Contains(typeof(float));
    }

    [Test] public async Task Text_DeclaresString()
    {
        await Assert.That(Clrs(global::app.type.item.text.@this.OwnedClrTypes)).Contains(typeof(string));
    }

    [Test] public async Task Path_DeclaresPathSubclasses()
    {
        // path declares its base type Assignable — every scheme subclass routes
        // to path. Pin the declaration and that a concrete subclass resolves.
        var pathDecl = global::app.type.path.@this.OwnedClrTypes;
        await Assert.That(pathDecl.Any(o => o.Assignable && o.Clr == typeof(global::app.type.path.@this))).IsTrue();
        var (family, _) = global::app.type.convert.@this.OwnerOf(typeof(global::app.type.path.file.@this));
        await Assert.That(family).IsEqualTo(typeof(global::app.type.path.@this));
    }

    // Flipped from the original `Image_DeclaresByteArrayForPngGifJpeg`
    // (test-designer open item #flip): OwnerOf keys on the conversion *target*,
    // so declaring byte[] would hijack every byte[]-target conversion into image
    // construction. image owns its own wrapper type (matching the old
    // self-owning Discover arm); raw bytes are decoded by image.Read, not by
    // routing the byte[] CLR target to image.
    [Test] public async Task Image_DeclaresOwnWrapperType_NotByteArrayTarget()
    {
        await Assert.That(Clrs(global::app.type.image.@this.OwnedClrTypes))
            .Contains(typeof(global::app.type.image.@this));
        var (family, _) = global::app.type.convert.@this.OwnerOf(typeof(byte[]));
        await Assert.That(family).IsNotEqualTo(typeof(global::app.type.image.@this));
    }

    // Probe — ask which family owns a CLR type; assert the answer is the family
    // that declares it (not a hand-written branch). int is owned in Stage 1;
    // Stage 2 extends number's declaration to uint/ulong/Int128/BigInteger and
    // this composition picks them up with no convert/this.cs edit.
    [Test] public async Task OwnerOf_RoutingComposes_FromFamilyDeclarations()
    {
        var (family, kind) = global::app.type.convert.@this.OwnerOf(typeof(long));
        await Assert.That(family).IsEqualTo(typeof(global::app.type.number.@this));
        await Assert.That(kind).IsEqualTo("long");
        await Assert.That(Clrs(global::app.type.number.@this.OwnedClrTypes)).Contains(typeof(long));
    }
}
