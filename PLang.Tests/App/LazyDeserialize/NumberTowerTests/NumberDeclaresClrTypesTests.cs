using TUnit.Core;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace PLang.Tests.App.LazyDeserialize.NumberTowerTests;

// The distributed-OwnerOf payoff, scoped to number. Stage 1 carved the
// central switch onto each family; Stage 2 then *extends* number's
// declaration to include the full tower. Adding `uint`/`ulong`/`Int128`/
// `BigInteger` must touch only `number`.
public class NumberDeclaresClrTypesTests
{
    // The catalog asserted via the family declaration surface (whichever
    // shape Stage 1 chose for the per-family declaration).
    [Test] public async Task Number_DeclaresFullTowerCrlTypes() { throw new System.NotImplementedException("not implemented"); }

    // Architectural probe — walk the OwnerOf composition and assert no
    // central switch over CLR types exists. Adding a synthetic family-owned
    // CLR type at runtime should be picked up by the registry without a
    // change to a central file.
    [Test] public async Task Number_AddingNewCrlType_RequiresOnlyNumberEdit() { throw new System.NotImplementedException("not implemented"); }
}
