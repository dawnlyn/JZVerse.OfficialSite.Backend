namespace JZVerse.MicroHuaxia.Observability.Core.Formatting;

/// <summary>
/// 鲜艳 ANSI 颜色常量（使用 Bright/Bold 变体）
/// </summary>
public static class AnsiColors
{
    public const string Reset = "\x1b[0m";

    // 亮色前景
    public const string BrightGreen = "\x1b[92m";
    public const string BrightRed = "\x1b[91m";
    public const string BrightYellow = "\x1b[93m";
    public const string BrightCyan = "\x1b[96m";
    public const string BrightMagenta = "\x1b[95m";
    public const string BrightBlue = "\x1b[94m";
    public const string BrightWhite = "\x1b[97m";

    // 粗体 + 亮白
    public const string BoldWhite = "\x1b[1;97m";
    public const string Bold = "\x1b[1m";
}
