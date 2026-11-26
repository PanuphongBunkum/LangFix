namespace LangFix;

using System.Reflection;

internal static class WordDictionaryService
{
    private static readonly HashSet<string> English = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> Thai = new(StringComparer.Ordinal);

    public static bool BuiltInDictionariesLoaded { get; }
    public static bool RightLangDictionariesLoaded { get; }
    public static string SourceDescription => RightLangDictionariesLoaded
        ? $"LangFix + RightLang ({English.Count:N0} EN / {Thai.Count:N0} TH)"
        : $"LangFix ({English.Count:N0} EN / {Thai.Count:N0} TH)";

    static WordDictionaryService()
    {
        var builtInEnglish = LoadEmbedded("en_US.dic", English);
        var builtInThai = LoadEmbedded("th_TH.dic", Thai);
        BuiltInDictionariesLoaded = builtInEnglish && builtInThai && English.Count > 10_000 && Thai.Count > 10_000;

        if (Environment.GetEnvironmentVariable("LANGFIX_DISABLE_EXTERNAL_DICTIONARIES") == "1") return;

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var folder = Path.Combine(programFilesX86, "RightLang", "dict");
        var englishBefore = English.Count;
        var thaiBefore = Thai.Count;
        var loaded = Load(Path.Combine(folder, "eng.txt"), English);
        loaded |= Load(Path.Combine(folder, "thai.txt"), Thai);
        Load(Path.Combine(folder, "user.en.txt"), English);
        Load(Path.Combine(folder, "user.th.txt"), Thai);
        RightLangDictionariesLoaded = loaded && (English.Count > englishBefore || Thai.Count > thaiBefore);
    }

    private static bool LoadEmbedded(string fileName, HashSet<string> destination)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames()
                .SingleOrDefault(name => name.EndsWith(fileName, StringComparison.OrdinalIgnoreCase));
            if (resourceName is null) return false;
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null) return false;
            using var reader = new StreamReader(stream);
            var added = 0;
            while (reader.ReadLine() is { } line)
            {
                var word = ParseHunspellWord(line);
                if (word.Length > 0 && destination.Add(word)) added++;
            }
            return added > 0;
        }
        catch { return false; }
    }

    private static string ParseHunspellWord(string line)
    {
        var word = line.Trim();
        if (int.TryParse(word, out _)) return "";
        var tab = word.IndexOf('\t');
        if (tab >= 0) word = word[..tab];
        var slash = word.IndexOf('/');
        if (slash >= 0) word = word[..slash];
        return word.Trim();
    }

    private static bool Load(string path, HashSet<string> destination)
    {
        try
        {
            if (!File.Exists(path)) return false;
            var added = 0;
            foreach (var line in File.ReadLines(path))
            {
                var word = line.Trim();
                if (word.Length > 0 && destination.Add(word)) added++;
            }
            return added > 0;
        }
        catch { return false; }
    }

    public static bool IsEnglishKnown(string value)
    {
        var word = NormalizeEnglish(value);
        if (word.Length == 0) return false;
        if (English.Contains(word)) return true;
        for (var length = Math.Min(8, word.Length); length >= 3; length--)
        {
            if (English.Contains(word[..length]) && word.Length - length <= 3) return true;
        }
        return false;
    }

    public static bool IsEnglishExact(string value)
    {
        var word = NormalizeEnglish(value);
        return word.Length > 0 && English.Contains(word);
    }

    public static bool IsThaiKnown(string value)
    {
        var word = NormalizeThai(value);
        if (word.Length == 0) return false;
        if (Thai.Contains(word)) return true;
        return CanComposeThai(word);
    }

    public static bool IsThaiExact(string value)
    {
        var word = NormalizeThai(value);
        return word.Length > 0 && Thai.Contains(word);
    }

    private static bool CanComposeThai(string word)
    {
        var reachable = new bool[word.Length + 1];
        reachable[0] = true;
        for (var start = 0; start < word.Length; start++)
        {
            if (!reachable[start]) continue;
            for (var end = start + 2; end <= word.Length; end++)
            {
                if (Thai.Contains(word[start..end])) reachable[end] = true;
            }
        }
        return reachable[word.Length];
    }

    private static string NormalizeEnglish(string value) => value.Trim().TrimEnd('.', ',', ';', ':', '!', '?').ToLowerInvariant();
    private static string NormalizeThai(string value) => value.Trim().TrimEnd('.', ',', ';', ':', '!', '?', 'ๆ', 'ฯ');
}
