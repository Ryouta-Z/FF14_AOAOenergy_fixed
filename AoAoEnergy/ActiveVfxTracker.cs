namespace AoAoEnergy;

internal sealed class ActiveVfxTracker
{
    private readonly object gate = new();
    private readonly HashSet<nint> handles = new();
    private readonly HashSet<nint> claimedForRemoval = new();

    public void Track(nint handle)
    {
        if (handle == nint.Zero)
            return;

        lock (gate)
            handles.Add(handle);
    }

    public bool ShouldProcessNaturalRemoval(nint handle)
    {
        lock (gate)
        {
            if (claimedForRemoval.Contains(handle))
                return false;

            handles.Remove(handle);
            return true;
        }
    }

    public IReadOnlyList<nint> ClaimAllForRemoval()
    {
        lock (gate)
        {
            var activeHandles = handles.ToArray();
            handles.Clear();
            claimedForRemoval.UnionWith(activeHandles);
            return activeHandles;
        }
    }

    public void CompleteRemoval(nint handle)
    {
        lock (gate)
            claimedForRemoval.Remove(handle);
    }
}
