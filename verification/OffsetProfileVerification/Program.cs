using AoAoEnergy;

static void Equal(int expected, int actual, string behavior)
{
    if (expected != actual)
        throw new InvalidOperationException($"{behavior}: expected {expected}, got {actual}");
}

var profiles = new OffsetProfileStore();
Equal(0, profiles.GetOffsetIndex(100), "new users start at the original position");

profiles.SetOffsetIndex(100, 4);
Equal(4, profiles.GetOffsetIndex(200), "global mode applies the selected position to every character");

profiles.RememberPerCharacter = true;
profiles.SetOffsetIndex(100, 2);
Equal(2, profiles.GetOffsetIndex(100), "character mode remembers the current character");
Equal(4, profiles.GetOffsetIndex(200), "unknown characters fall back to the global position");

profiles.SetOffsetIndex(100, 99);
Equal(5, profiles.GetOffsetIndex(100), "position indices are clamped to the highest preset");

profiles.SetOffsetIndex(100, -10);
Equal(0, profiles.GetOffsetIndex(100), "position indices are clamped to the original preset");

profiles.CharacterOffsetIndices = null!;
Equal(4, profiles.GetOffsetIndex(100), "a null character map from an old or damaged config falls back safely");

var activeVfx = new ActiveVfxTracker();
activeVfx.Track((nint)0x111);
activeVfx.Track((nint)0x222);
activeVfx.Track((nint)0x222);
Equal(1, activeVfx.ShouldProcessNaturalRemoval((nint)0x111) ? 1 : 0, "natural removal owns a live tracked VFX");
var handlesToRemove = activeVfx.ClaimAllForRemoval();
Equal(1, handlesToRemove.Count, "only live unique VFX instances are cleared");
Equal(0x222, (int)handlesToRemove[0], "the remaining VFX handle is returned for removal");
Equal(0, activeVfx.ShouldProcessNaturalRemoval((nint)0x222) ? 1 : 0, "natural removal cannot race a VFX claimed by clear");
activeVfx.CompleteRemoval((nint)0x222);
Equal(1, activeVfx.ShouldProcessNaturalRemoval((nint)0x999) ? 1 : 0, "unrelated VFX removal is never blocked");
Equal(0, activeVfx.ClaimAllForRemoval().Count, "clearing the VFX list is idempotent");

Console.WriteLine("PASS offset profile behavior");
