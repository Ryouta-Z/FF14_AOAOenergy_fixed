namespace AoAoEnergy;

internal sealed class OffsetProfileStore
{
    private Dictionary<ulong, int> characterOffsetIndices = new();

    internal const int MinimumOffsetIndex = 0;
    internal const int MaximumOffsetIndex = 5;

    public bool RememberPerCharacter { get; set; }
    public int GlobalOffsetIndex { get; set; }
    public Dictionary<ulong, int> CharacterOffsetIndices
    {
        get => characterOffsetIndices;
        set => characterOffsetIndices = value ?? new Dictionary<ulong, int>();
    }

    public int GetOffsetIndex(ulong contentId)
    {
        if (RememberPerCharacter && contentId != 0 && CharacterOffsetIndices.TryGetValue(contentId, out var characterIndex))
            return Clamp(characterIndex);

        return Clamp(GlobalOffsetIndex);
    }

    public void SetOffsetIndex(ulong contentId, int offsetIndex)
    {
        var clamped = Clamp(offsetIndex);
        if (RememberPerCharacter && contentId != 0)
            CharacterOffsetIndices[contentId] = clamped;
        else
            GlobalOffsetIndex = clamped;
    }

    private static int Clamp(int offsetIndex)
        => Math.Clamp(offsetIndex, MinimumOffsetIndex, MaximumOffsetIndex);
}
