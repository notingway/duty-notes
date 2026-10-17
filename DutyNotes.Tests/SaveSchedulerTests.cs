namespace DutyNotes.Tests;

/// <summary>When a pending save is written: after a quiet moment, never later than the longest wait, and not at all when nothing changed.</summary>
public sealed class SaveSchedulerTests : CheckBase
{
    [Fact]
    public void NothingIsPendingUntilSomethingChanges()
    {
        var s = new SaveScheduler(quietSeconds: 0.5, maxWaitSeconds: 5);
        Check("a new scheduler has nothing pending", !s.Pending);
        Check("...and nothing is ever due", !s.IsDue(0) && !s.IsDue(1000));
    }

    [Fact]
    public void ASingleChangeIsDueAfterTheQuietTime()
    {
        var s = new SaveScheduler(0.5, 5);
        s.MarkChanged(10);
        Check("a change is pending", s.Pending);
        Check("it is not due straight away", !s.IsDue(10));
        Check("it is not due just before the quiet time is up", !s.IsDue(10.49));
        Check("it is due once the quiet time has passed", s.IsDue(10.5));
        Check("and it stays due until it is saved", s.IsDue(20));
    }

    [Fact]
    public void ABurstOfChangesIsOneSave()
    {
        // Dragging a slider changes the setting every frame (here every 0.1 s, for a second).
        var s = new SaveScheduler(0.5, 5);
        for (var t = 0.0; t <= 1.0001; t += 0.1) s.MarkChanged(t);

        Check("while the changes keep coming, nothing is due", !s.IsDue(1.0) && !s.IsDue(1.3));
        Check("it is due a quiet time after the last change", s.IsDue(1.5));
    }

    [Fact]
    public void ALongRunOfChangesIsStillSavedEventually()
    {
        // Someone typing or dragging without a pause for a long time must not put the save off for ever.
        var s = new SaveScheduler(0.5, 5);
        double? dueAt = null;
        for (var t = 0.0; t <= 20; t += 0.1)
        {
            s.MarkChanged(t);
            if (dueAt is null && s.IsDue(t)) dueAt = t;
        }
        Check("a save becomes due even though the changes never paused", dueAt is not null);
        Check("it becomes due when the longest wait is reached", dueAt is { } d && Math.Abs(d - 5.0) < 0.15, dueAt?.ToString("0.0") ?? "never");
    }

    [Fact]
    public void SavingClearsWhatWasPending()
    {
        var s = new SaveScheduler(0.5, 5);
        s.MarkChanged(1);
        s.Saved();
        Check("after a save nothing is pending", !s.Pending);
        Check("...and nothing is due", !s.IsDue(100));
    }

    [Fact]
    public void AfterASaveTheNextChangeStartsAFreshWait()
    {
        var s = new SaveScheduler(0.5, 5);
        s.MarkChanged(0);
        s.MarkChanged(4.9);
        s.Saved();

        s.MarkChanged(10);
        Check("the old changes do not count towards the longest wait", !s.IsDue(10.3));
        Check("the new change is due after the quiet time", s.IsDue(10.5));
    }

    [Fact]
    public void ASaveByAnyoneElseAlsoCoversThePendingChange()
    {
        // The plugin saves at once for a click, and that write already contains everything, including what was waiting.
        var s = new SaveScheduler(0.5, 5);
        s.MarkChanged(0);
        s.Saved();
        Check("a change that was waiting is not saved a second time", !s.Pending && !s.IsDue(1));
    }
}