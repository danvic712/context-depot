using System.Globalization;
using System.Text;

namespace ContextDepot.Application.Documents;

internal static class MarkdownHeadingSlugger
{
    public static string Slug(string text)
    {
        // JavaScript uses full Unicode lowercase mapping: dotted I expands, and
        // a capital sigma after a cased letter becomes final sigma at a word end.
        var lower = new StringBuilder();
        var runes = text.EnumerateRunes().ToArray();
        for (var index = 0; index < runes.Length; index++)
        {
            if (runes[index].Value == 0x0130) lower.Append("i\u0307");
            else if (runes[index].Value == 0x03A3 && HasCasedLetter(runes, index, -1) && !HasCasedLetter(runes, index, 1))
                lower.Append('\u03C2');
            else lower.Append(Rune.ToLowerInvariant(runes[index]).ToString());
        }
        var slug = new StringBuilder(lower.Length);
        foreach (var rune in lower.ToString().EnumerateRunes())
        {
            if (rune.Value == ' ') slug.Append('-');
            else if (rune.Value == '-' || rune.Value is >= 0x24B6 and <= 0x24E9 ||
                Rune.GetUnicodeCategory(rune) is UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
                    UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or UnicodeCategory.OtherLetter or
                    UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or
                    UnicodeCategory.DecimalDigitNumber or UnicodeCategory.LetterNumber or UnicodeCategory.ConnectorPunctuation)
                slug.Append(rune.ToString());
        }
        return slug.ToString();
    }

    private static bool HasCasedLetter(Rune[] runes, int index, int direction)
    {
        for (index += direction; index >= 0 && index < runes.Length; index += direction)
        {
            var rune = runes[index];
            var category = Rune.GetUnicodeCategory(rune);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format or
                UnicodeCategory.ModifierLetter or UnicodeCategory.ModifierSymbol ||
                rune.Value is 0x0027 or 0x002E or 0x003A or 0x00B7 or 0x0387 or 0x05F4 or 0x2018 or 0x2019 or 0x2024 or 0x2027 or 0xFE13 or 0xFE52 or 0xFE55 or 0xFF07 or 0xFF0E or 0xFF1A)
                continue;
            return Rune.ToLowerInvariant(rune) != Rune.ToUpperInvariant(rune);
        }
        return false;
    }
}
