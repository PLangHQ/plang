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
    // The central switch is either deleted or routes through declarations.
    // Pinned by *behaviour*: adding a new family-owned CLR type (Stage 2 adds
    // uint/ulong/Int128/BigInteger to number) requires editing only the
    // family — never app/type/convert/this.cs.
    [Test] public async Task OwnerOf_CentralSwitch_NoLongerExists() { throw new System.NotImplementedException("not implemented"); }

    [Test] public async Task Number_DeclaresIntLongDecimalDoubleFloat() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Text_DeclaresString() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Path_DeclaresPathSubclasses() { throw new System.NotImplementedException("not implemented"); }
    [Test] public async Task Image_DeclaresByteArrayForPngGifJpeg() { throw new System.NotImplementedException("not implemented"); }

    // Probe — ask the registry which family owns `typeof(uint)`; assert
    // `number`. Pin that the answer comes from the family's declaration, not
    // a hand-written branch in the central switch.
    [Test] public async Task OwnerOf_RoutingComposes_FromFamilyDeclarations() { throw new System.NotImplementedException("not implemented"); }
}
