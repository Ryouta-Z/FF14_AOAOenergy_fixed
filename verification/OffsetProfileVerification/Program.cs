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

Console.WriteLine("PASS offset profile behavior");
