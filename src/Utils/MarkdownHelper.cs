using System.Text;

namespace Yan.Utils;

internal static class MarkdownHelper
{
    public static string Escape(this string input)
    {
        return new StringBuilder(input).Replace("_", "\\_").Replace("*", "\\*").Replace("[", "\\[").Replace("]", "\\]")
            .Replace("(", "\\(").Replace(")", "\\)").Replace("~", "\\~").Replace("`", "\\`").Replace(">", "\\>")
            .Replace("#", "\\#").Replace("+", "\\+").Replace("-", "\\-").Replace("=", "\\=").Replace("|", "\\|")
            .Replace("{", "\\{").Replace("}", "\\}").Replace(".", "\\.").Replace("!", "\\!").ToString();
    }
}