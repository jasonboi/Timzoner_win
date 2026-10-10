namespace Timzoner.Core;

public static class AppText
{
    private static string language = "zh-CN";

    public static string Language
    {
        get => Volatile.Read(ref language);
        set => Volatile.Write(ref language, value);
    }

    public static bool IsEnglish => Language == "en";
    public static string Get(string chinese, string english) => IsEnglish ? english : chinese;
}
