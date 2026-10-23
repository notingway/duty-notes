using System.Text.RegularExpressions;
using Lumina.Text;
using Lumina.Text.Expressions;
using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;

namespace DutyNotes;

/// <summary>
/// A skill's description holds its text for every level and job, with switches such as "if the job is warrior and the level is at least 78"
/// in it: the game picks a branch from your current job and level when it draws a tooltip, so outside a duty you only ever see the max-level
/// text. <see cref="AtLevel"/> answers those switches with a level and job of our choosing, so the overlay can show a skill as it is at the
/// encounter's level. Everything else in the text (colours, switches on anything else) is kept as it is, for Dalamud's evaluator to fill in.
/// </summary>
public static partial class SyncedText
{
    // The global numbers the switches read, as the game data writes them (gnum72 and gnum68). See docs/GAME-DATA.md.
    public const uint LevelParameter = 72;
    public const uint JobParameter = 68;

    /// <param name="job">ClassJob row id. 0 leaves the job switches to the game, which answers them with your current job.</param>
    public static ReadOnlySeString AtLevel(ReadOnlySeStringSpan text, int level, uint job)
    {
        var sb = new SeStringBuilder();
        Append(sb, text, new Answers(level, job));
        return sb.ToReadOnlySeString();
    }

    /// <summary>
    /// What a trait's text says it sets recast times to: "Reduces Troubadour recast time to 90 seconds." The Action sheet has the recast from
    /// before any trait, so the tooltip lowers it with these. Takes the trait's plain text (English).
    /// </summary>
    public static List<(string Action, float Seconds)> RecastsSetBy(string traitText)
    {
        var found = new List<(string, float)>();
        foreach (Match m in RecastRegex().Matches(traitText))
        {
            var seconds = float.Parse(m.Groups["s"].Value, System.Globalization.CultureInfo.InvariantCulture);
            foreach (var name in Names(m.Groups["names"].Value)) found.Add((name, seconds));
        }
        return found;
    }

    /// <summary>
    /// What a trait's text says it sets the charges of a skill to: "Allows the accumulation of charges for consecutive uses of Aurora. Maximum Charges: 2"
    /// or "Allows a third charge of Essential Dignity." The Action sheet has the charges from before any trait. Takes the trait's plain text (English).
    /// </summary>
    public static List<(string Action, int Charges)> ChargesSetBy(string traitText)
    {
        var found = new List<(string, int)>();
        foreach (Match m in AccumulateRegex().Matches(traitText))
            foreach (var name in Names(m.Groups["names"].Value))
                found.Add((name, int.Parse(m.Groups["n"].Value, System.Globalization.CultureInfo.InvariantCulture)));
        foreach (Match m in NthChargeRegex().Matches(traitText))
        {
            var charges = m.Groups["nth"].Value.ToLowerInvariant() switch { "second" => 2, "third" => 3, _ => 4 };
            foreach (var name in Names(m.Groups["names"].Value)) found.Add((name, charges));
        }
        return found;
    }

    private static IEnumerable<string> Names(string list) =>
        NameSplitRegex().Split(list).Select(n => n.Trim()).Where(n => n.Length > 0);

    [GeneratedRegex(@"accumulation of charges for consecutive uses of (?<names>[^.]+?)\.\s*Maximum Charges:\s*(?<n>\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex AccumulateRegex();

    [GeneratedRegex(@"Allows a (?<nth>second|third|fourth) charge of (?<names>[^.]+?)\.", RegexOptions.IgnoreCase)]
    private static partial Regex NthChargeRegex();

    [GeneratedRegex(@"Reduces (?<names>[^.]+?) recast (?:time|timer) to (?<s>\d+(?:\.\d+)?) seconds")]
    private static partial Regex RecastRegex();

    [GeneratedRegex(@",\s*(?:and\s+)?|\s+and\s+")]
    private static partial Regex NameSplitRegex();

    private readonly record struct Answers(int Level, uint Job);

    private static void Append(SeStringBuilder sb, ReadOnlySeStringSpan text, Answers answers)
    {
        foreach (var payload in text)
        {
            // An "if" on the level or job is replaced by the branch it picks (itself checked for more switches). Any other "if" is kept whole,
            // so a switch nested inside it is left for the game, which answers it with your real level and job.
            if (payload.Type == ReadOnlySePayloadType.Macro && payload.MacroCode == MacroCode.If
                && payload.TryGetExpression(out var condition, out var whenTrue, out var whenFalse)
                && TryDecide(condition, answers, out var result)
                && TryAppendBranch(sb, result ? whenTrue : whenFalse, answers))
                continue;

            sb.Append(payload);
        }
    }

    private static bool TryAppendBranch(SeStringBuilder sb, ReadOnlySeExpressionSpan branch, Answers answers)
    {
        if (branch.TryGetString(out var text))
        {
            Append(sb, text, answers);
            return true;
        }
        if (branch.TryGetUInt(out var number))
        {
            sb.Append(number.ToString());
            return true;
        }
        return false;
    }

    /// <summary>Answers a comparison whose sides are numbers, the level or the job. Anything else is not ours to answer.</summary>
    private static bool TryDecide(ReadOnlySeExpressionSpan condition, Answers answers, out bool result)
    {
        result = false;
        if (!condition.TryGetBinaryExpression(out var type, out var left, out var right)) return false;
        if (!TryValue(left, answers, out var a, out var leftIsOurs) || !TryValue(right, answers, out var b, out var rightIsOurs)) return false;
        if (!leftIsOurs && !rightIsOurs) return false;

        switch ((ExpressionType)type)
        {
            case ExpressionType.GreaterThanOrEqualTo: result = a >= b; return true;
            case ExpressionType.GreaterThan: result = a > b; return true;
            case ExpressionType.LessThanOrEqualTo: result = a <= b; return true;
            case ExpressionType.LessThan: result = a < b; return true;
            case ExpressionType.Equal: result = a == b; return true;
            case ExpressionType.NotEqual: result = a != b; return true;
            default: return false;
        }
    }

    private static bool TryValue(ReadOnlySeExpressionSpan e, Answers answers, out long value, out bool isOurs)
    {
        value = 0;
        isOurs = false;
        if (e.TryGetUInt(out var number))
        {
            value = number;
            return true;
        }
        if (!e.TryGetParameterExpression(out var type, out var operand) || (ExpressionType)type != ExpressionType.GlobalNumber
            || !operand.TryGetUInt(out var index))
            return false;

        if (index == LevelParameter) value = answers.Level;
        else if (index == JobParameter && answers.Job != 0) value = answers.Job;
        else return false;
        isOurs = true;
        return true;
    }
}
