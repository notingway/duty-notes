using Xunit.Sdk;

namespace DutyNotes.Tests;

/// <summary>
/// The base of every test class. A test calls <see cref="Check"/> as many times as it likes, with the label that says what should be true.
/// A check that fails does not stop the test, so one run shows every check that is wrong, not just the first; the test fails when it
/// ends, listing each failed label. (xunit makes a new object for every test and disposes it afterwards, which is where this reports.)
/// </summary>
public abstract class CheckBase : IDisposable
{
    private readonly List<string> failures = new();

    protected void Check(string label, bool ok, string detail = "")
    {
        if (!ok) failures.Add(detail.Length > 0 ? $"{label}   [{detail}]" : label);
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
        if (failures.Count > 0)
            throw new XunitException($"{failures.Count} check(s) failed:{Environment.NewLine}  - " + string.Join($"{Environment.NewLine}  - ", failures));
    }
}
