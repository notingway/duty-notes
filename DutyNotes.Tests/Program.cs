namespace DutyNotes.Tests;

/// <summary>A tiny test runner: suites of named checks, a summary, and an exit code.</summary>
internal static class T
{
    public static int Passed { get; private set; }
    public static int Failed { get; private set; }

    public static void Suite(string name, Action body)
    {
        Console.WriteLine($"\n== {name}");
        try
        {
            body();
        }
        catch (Exception ex)
        {
            Failed++;
            Console.WriteLine($"FAIL  the suite threw {ex.GetType().Name}: {ex.Message}");
        }
    }

    public static void Check(string label, bool ok, string detail = "")
    {
        if (ok) Passed++; else Failed++;
        Console.WriteLine($"{(ok ? "ok  " : "FAIL")}  {label}{(detail.Length > 0 ? "   [" + detail + "]" : "")}");
    }
}

internal static class Program
{
    private static int Main()
    {
        T.Suite("Navigation between phases", NavigationTests.Run);
        T.Suite("Settings, defaults, reset and overlay visibility", SettingsTests.Run);
        T.Suite("Saving and loading settings (Dalamud's way)", SerializationTests.Run);
        T.Suite("Export and import", SharingTests.Run);
        T.Suite("Help: the command list", HelpTests.Run);


        Console.WriteLine($"\n{T.Passed} passed, {T.Failed} failed");
        return T.Failed == 0 ? 0 : 1;
    }
}
