namespace DutyNotes;

/// <summary>
/// Decides when a pending save should be written. Dragging a slider or typing changes the settings every frame, and writing the whole
/// settings file that often is wasteful, so those changes wait for a short quiet moment first. A long, unbroken run of changes is still
/// saved every so often, so a crash never loses more than a few seconds of editing. Pure (no game code), so it can be tested.
/// Times are in seconds, from any clock that only goes forward.
/// </summary>
internal sealed class SaveScheduler
{
    private readonly double quietSeconds;
    private readonly double maxWaitSeconds;
    private double? firstChange;
    private double? lastChange;

    /// <param name="quietSeconds">How long nothing may change before the save is due.</param>
    /// <param name="maxWaitSeconds">The longest a save may be put off while changes keep coming.</param>
    public SaveScheduler(double quietSeconds = 0.5, double maxWaitSeconds = 5)
    {
        this.quietSeconds = quietSeconds;
        this.maxWaitSeconds = maxWaitSeconds;
    }

    /// <summary>Whether something has changed since the last save.</summary>
    public bool Pending => firstChange is not null;

    /// <summary>Something changed. Every change restarts the quiet wait.</summary>
    public void MarkChanged(double now)
    {
        firstChange ??= now;
        lastChange = now;
    }

    /// <summary>True when a change is waiting and either nothing has changed for the quiet time, or it has waited as long as it may.</summary>
    public bool IsDue(double now)
        => firstChange is { } first && lastChange is { } last
           && (now - last >= quietSeconds || now - first >= maxWaitSeconds);

    /// <summary>The settings were saved (by this scheduler's owner, or by anyone else): nothing is pending any more.</summary>
    public void Saved()
    {
        firstChange = null;
        lastChange = null;
    }
}