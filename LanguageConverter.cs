namespace LangFix;

internal static class LanguageConverter
{
    private const string English = "`1234567890-=qwertyuiop[]asdfghjkl;'zxcvbnm,./~!@#$%^&*()_+QWERTYUIOP{}ASDFGHJKL:\"ZXCVBNM<>?";
    private const string Thai =    "_ๅ/-ภถุึคตจขชๆไำพะัีรนยบลฟหกดเ้่าสวงผปแอิืทมใฝ%+๑๒๓๔ู฿๕๖๗๘๙๐\"ฎฑธํ๊ณฯญฐ,ฤฆฏโฌ็๋ษศซ.()ฉฮฺ์?ฒฬฦ";

    private static readonly Dictionary<char, char> EnToTh = English.Zip(Thai).ToDictionary(x => x.First, x => x.Second);
    private static readonly Dictionary<char, char> ThToEn = Thai.Zip(English).GroupBy(x => x.First).ToDictionary(g => g.Key, g => g.First().Second);

    private static readonly HashSet<string> EnglishWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "a","about","all","also","and","app","are","as","at","back","be","because","but","by","can","code","computer",
        "day","do","email","english","file","for","from","get","go","good","have","hello","help","here","how","i","if","in",
        "is","it","just","key","language","like","me","more","my","need","new","no","not","now","of","ok","on","one","or",
        "please","right","see","so","some","test","text","thank","that","the","then","there","this","time","to","today","type","use",
        "want","we","what","when","where","will","windows","with","work","would","yes","you","your"
    };

    private static readonly HashSet<string> ThaiWords = new(StringComparer.Ordinal)
    {
        "ก็","ของ","ขอบคุณ","ขอ","ครับ","ค่ะ","คอมพิวเตอร์","คือ","จะ","จาก","ฉัน","ช่วย","ดี","ได้","ต้องการ","ตอนนี้","ไทย",
        "ทำ","ที่","นะ","นี้","ใน","เป็น","โปรแกรม","พิมพ์","ภาษา","มี","ไม่","เรา","สวัสดี","สำหรับ","หรือ","อยาก","อะไร","อังกฤษ",
        "อัน","อยู่","แล้ว","และ","ให้","ใช่","ใช้","ไหม","ใหม่"
    };

    public static bool IsConvertible(string text) => text.Any(EnToTh.ContainsKey) || text.Any(ThToEn.ContainsKey);

    public static string Convert(string text)
    {
        var thaiCount = text.Count(IsThai);
        var latinCount = text.Count(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
        var map = thaiCount > latinCount ? ThToEn : EnToTh;
        return new string(text.Select(c => map.TryGetValue(c, out var converted) ? converted : c).ToArray());
    }

    public static bool ShouldAutoConvert(string token, out string converted)
    {
        converted = Convert(token);
        if (converted == token || token.Length < 2) return false;

        var hasThai = token.Any(IsThai);
        var hasLatin = token.Any(c => c is >= 'A' and <= 'Z' or >= 'a' and <= 'z');
        if (hasThai && hasLatin) return false;

        if (hasLatin)
        {
            if (IsKnownEnglish(token)) return false;
            if (IsKnownThai(converted)) return true;
            return token.Length >= 4 && EnglishScore(token) <= 0 && ThaiScore(converted) >= 3;
        }

        if (hasThai)
        {
            if (IsKnownThai(token)) return false;
            if (IsKnownEnglish(converted)) return true;
            return token.Length >= 4 && ThaiScore(token) <= 0 && EnglishScore(converted) >= 3;
        }
        return false;
    }

    internal static bool IsKnownEnglish(string value) => EnglishWords.Contains(value) || WordDictionaryService.IsEnglishKnown(value);
    internal static bool IsKnownThai(string value) => ThaiWords.Contains(value) || WordDictionaryService.IsThaiKnown(value);
    internal static bool IsExactKnownEnglish(string value) => EnglishWords.Contains(value) || WordDictionaryService.IsEnglishExact(value);
    internal static bool IsExactKnownThai(string value) => ThaiWords.Contains(value) || WordDictionaryService.IsThaiExact(value);

    private static bool IsThai(char c) => c is >= '\u0E00' and <= '\u0E7F';

    private static int EnglishScore(string value)
    {
        var s = value.ToLowerInvariant();
        var score = s.Any("aeiouy".Contains) ? 2 : -3;
        string[] common = ["th","he","in","er","an","re","on","at","en","nd","ing","tion","ou","ea","is"];
        score += common.Count(s.Contains);
        if (s.Contains("q") && !s.Contains("qu")) score -= 2;
        if (s.Any(c => !char.IsLetter(c) && c != '\'')) score -= 2;
        return score;
    }

    private static int ThaiScore(string value)
    {
        var score = value.Any(c => "าิีึืุูเแโใไัำ".Contains(c)) ? 2 : -2;
        string[] common = ["กา","ที่","เป็น","ไม่","ได้","การ","ความ","สว","วั","ดี","ภาษา","ครับ","ค่ะ","อย","ใช้"];
        score += common.Count(value.Contains);
        if (value.StartsWithAny("่","้","๊","๋","์","ั","ิ","ี","ึ","ื","ุ","ู")) score -= 4;
        return score;
    }

    private static bool StartsWithAny(this string value, params string[] starts) => starts.Any(value.StartsWith);
}
