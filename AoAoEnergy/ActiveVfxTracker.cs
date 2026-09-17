namespace AoAoEnergy;

internal sealed class ActiveVfxTracker
{
    private readonly HashSet<nint> handles = new();

    public void Track(nint handle)
    {
        if (handle != nint.Zero)
            handles.Add(handle);
    }

    public void Untrack(nint handle) => handles.Remove(handle);

    public IReadOnlyList<nint> Drain()
    {
        var activeHandles = handles.ToArray();
        handles.Clear();
        return activeHandles;
    }
}
