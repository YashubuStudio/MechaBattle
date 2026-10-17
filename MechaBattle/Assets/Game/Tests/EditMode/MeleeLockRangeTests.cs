using System;
using NUnit.Framework;
using MechaBattle.Contracts;

namespace MechaBattle.Tests
{
    // User request 2026-10-16: lock-on should still be useful while melee is the active
    // weapon, even though melee can only actually hit at point-blank range. yellowLockRange
    // is a "track this target while closing distance" concept, distinct from the weapon's
    // own damage/swing range, so melee is allowed to set it further out than its range.
    public sealed class MeleeLockRangeTests
    {
        [Test]
        public void MeleeLoadoutAcceptsYellowLockRangeBeyondItsOwnSwingRange()
        {
            Assert.DoesNotThrow(() => new UnitLoadout("melee-lock", 500, 0, 10, 1,
                760, 3.4f, 0.31f, 20, 10, kind: WeaponKind.Melee, redLockRange: 3.4f, yellowLockRange: 45));
        }

        [Test]
        public void MeleeLoadoutStillRejectsRedLockRangeAtOrBeyondItsYellowLockRange()
        {
            Assert.Throws<ArgumentException>(() => new UnitLoadout("melee-lock", 500, 0, 10, 1,
                760, 3.4f, 0.31f, 20, 10, kind: WeaponKind.Melee, redLockRange: 45, yellowLockRange: 45));
        }

        [Test]
        public void RangedLoadoutStillRejectsYellowLockRangeAtOrBeyondItsOwnRange()
        {
            // Regression: the melee exemption must not loosen the rule for any other kind.
            Assert.Throws<ArgumentException>(() => new UnitLoadout("ranged-lock", 500, 0, 10, 50,
                40, 80, 0.2f, 20, 10, kind: WeaponKind.Ranged, redLockRange: 24, yellowLockRange: 80));
        }
    }
}
