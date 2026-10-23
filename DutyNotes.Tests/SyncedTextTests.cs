using Lumina.Text;
using Lumina.Text.ReadOnly;

namespace DutyNotes.Tests;

/// <summary>
/// Skill descriptions answered for a chosen level and job (<see cref="SyncedText"/>). The texts are copied from the game data (October 2026,
/// patch 7.x), shortened, in the game's macro notation: gnum68 is the job, gnum72 the level.
/// </summary>
public class SyncedTextTests : CheckBase
{
    private const uint PLD = 19, WAR = 21, WHM = 24;

    private const string ThrillOfBattle =
        "Increases maximum HP by 20% and restores the amount increased.<br><if([gnum68==21],<if([gnum72>=78],<colortype(504)><edgecolortype(505)>Additional Effect: "
        + "<edgecolortype(0)><colortype(0)>Increases HP recovery via healing actions on self by 20%<br>,)>,)><colortype(504)><edgecolortype(505)>Duration:<edgecolortype(0)><colortype(0)> 10s";

    private const string Intervention = "Duration: <if([gnum68==19],<if([gnum72>=82],8,6)>,6)>s";

    private const string Reprisal =
        "Duration: <if([gnum72>=98],<if([gnum68==1],15,<if([gnum68==19],15,<if([gnum68==3],15,<if([gnum68==21],15,<if([gnum68==32],15,<if([gnum68==37],15,10)>)>)>)>)>)>,10)>s";

    private static ReadOnlySeString Parse(string macro) => new SeStringBuilder().AppendMacroString(macro).ToReadOnlySeString();

    private static string Plain(string macro, int level, uint job) => SyncedText.AtLevel(Parse(macro).AsSpan(), level, job).ExtractText();

    [Fact]
    public void LevelAndJobPickTheBranch()
    {
        Check("Thrill of Battle has no extra effect for a warrior below 78", !Plain(ThrillOfBattle, 70, WAR).Contains("Additional Effect"), Plain(ThrillOfBattle, 70, WAR));
        Check("Thrill of Battle gains the extra effect for a warrior at 78", Plain(ThrillOfBattle, 78, WAR).Contains("Additional Effect"), Plain(ThrillOfBattle, 78, WAR));
        Check("the extra effect is the warrior's only", !Plain(ThrillOfBattle, 100, PLD).Contains("Additional Effect"));
        Check("the rest of the text is kept", Plain(ThrillOfBattle, 70, WAR).Contains("Duration: 10s"), Plain(ThrillOfBattle, 70, WAR));

        Check("Intervention lasts 6s for a paladin at 80", Plain(Intervention, 80, PLD) == "Duration: 6s", Plain(Intervention, 80, PLD));
        Check("Intervention lasts 8s for a paladin at 82", Plain(Intervention, 82, PLD) == "Duration: 8s", Plain(Intervention, 82, PLD));

        Check("Reprisal lasts 10s at 90", Plain(Reprisal, 90, WAR) == "Duration: 10s", Plain(Reprisal, 90, WAR));
        Check("Reprisal lasts 15s for a tank at 100", Plain(Reprisal, 100, WAR) == "Duration: 15s", Plain(Reprisal, 100, WAR));
        Check("Reprisal's longer duration is for tanks only", Plain(Reprisal, 100, WHM) == "Duration: 10s", Plain(Reprisal, 100, WHM));
    }

    [Fact]
    public void WhatIsNotOursIsKept()
    {
        var colours = SyncedText.AtLevel(Parse(ThrillOfBattle).AsSpan(), 80, WAR).ToMacroString();
        Check("colours are kept", colours.Contains("<colortype(504)>"), colours);
        Check("no level or job switch is left once both are answered", !colours.Contains("<if("), colours);

        var noJob = SyncedText.AtLevel(Parse(Intervention).AsSpan(), 90, 0).ToMacroString();
        Check("with no job, the job switch is left for the game", noJob.Contains("gnum68"), noJob);

        var other = SyncedText.AtLevel(Parse("<if([gnum69>=50],A,B)>").AsSpan(), 90, WAR).ToMacroString();
        Check("a switch on another number is left as it is", other.Contains("gnum69"), other);

        Check("text with no switches comes back the same", Plain("Restores all of a target's HP.", 50, WHM) == "Restores all of a target's HP.");
    }

    [Fact]
    public void TraitsThatSetRecastTimes()
    {
        var troubadour = SyncedText.RecastsSetBy("Reduces Troubadour recast time to 90 seconds.");
        Check("one skill", troubadour.Count == 1 && troubadour[0] == ("Troubadour", 90f), string.Join(", ", troubadour));

        var two = SyncedText.RecastsSetBy("Reduces Hissatsu: Guren and Hissatsu: Senei recast time to 60 seconds.");
        Check("two skills joined by \"and\"", two.Count == 2 && two[0] == ("Hissatsu: Guren", 60f) && two[1] == ("Hissatsu: Senei", 60f), string.Join(", ", two));

        var timer = SyncedText.RecastsSetBy("Reduces Manafication recast timer to 110 seconds and adds an additional effect that increases magic damage dealt by 5%.");
        Check("\"recast timer\" counts too", timer.Count == 1 && timer[0] == ("Manafication", 110f), string.Join(", ", timer));

        Check("a reduction by some seconds is not a new recast", SyncedText.RecastsSetBy("Reduces Infuriate recast time by 5 seconds upon landing Fell Cleave.").Count == 0);
        Check("a haste trait is not a recast for one skill",
            SyncedText.RecastsSetBy("Reduces weaponskill cast time and recast time, spell cast time and recast time, and auto-attack delay by 10%.").Count == 0);
    }

    [Fact]
    public void TraitsThatSetCharges()
    {
        var aurora = SyncedText.ChargesSetBy("Allows the accumulation of charges for consecutive uses of Aurora.\nMaximum Charges: 2");
        Check("accumulation of charges", aurora.Count == 1 && aurora[0] == ("Aurora", 2), string.Join(", ", aurora));

        var third = SyncedText.ChargesSetBy("Allows a third charge of Bloodletter and Rain of Death.");
        Check("a third charge, for two skills", third.Count == 2 && third[0] == ("Bloodletter", 3) && third[1] == ("Rain of Death", 3), string.Join(", ", third));

        Check("stacks of a gauge are not charges", SyncedText.ChargesSetBy("Grants 1 stack of Addersgall automatically every twenty seconds.\nMaximum Stacks: 3").Count == 0);
    }
}
