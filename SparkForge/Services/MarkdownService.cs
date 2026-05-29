namespace SparkForge.Services;

public class MarkdownService
{
    /// <summary>
    /// Converts markdown to HTML without wrapping in a full page.
    /// Used by the chat renderer.
    /// </summary>
    public static string ConvertToHtml(string markdown)
    {
        return ConvertMarkdownToHtml(markdown);
    }

    /// <summary>
    /// Wraps markdown content in a full HTML page with Catppuccin theme
    /// for display in WebView2.
    /// </summary>
    public static string WrapInHtml(string markdownContent, string role)
    {
        var escapedContent = System.Web.HttpUtility.HtmlEncode(markdownContent);
        // Convert basic markdown to HTML
        var html = ConvertMarkdownToHtml(markdownContent);

        var bgColor = role == "user" ? "#45475a" : "#313244";
        var roleLabel = role == "user" ? "You" : "Assistant";

        return $@"<!DOCTYPE html>
<html>
<head>
<meta charset='utf-8'>
<style>
    * {{ margin: 0; padding: 0; box-sizing: border-box; }}
    body {{
        background: {bgColor};
        color: #cdd6f4;
        font-family: 'Segoe UI', -apple-system, sans-serif;
        font-size: 13px;
        line-height: 1.6;
        padding: 8px;
        overflow-x: hidden;
    }}
    pre {{
        background: #1e1e2e;
        padding: 12px;
        border-radius: 6px;
        overflow-x: auto;
        margin: 8px 0;
        border: 1px solid #45475a;
    }}
    code {{
        font-family: 'Cascadia Code', 'Fira Code', 'Consolas', monospace;
        font-size: 12px;
        background: #1e1e2e;
        padding: 2px 5px;
        border-radius: 3px;
    }}
    pre code {{
        background: none;
        padding: 0;
    }}
    p {{ margin: 4px 0; }}
    ul, ol {{ margin: 4px 0; padding-left: 20px; }}
    li {{ margin: 2px 0; }}
    blockquote {{
        border-left: 3px solid #89b4fa;
        padding-left: 12px;
        margin: 8px 0;
        color: #a6adc8;
    }}
    table {{
        border-collapse: collapse;
        margin: 8px 0;
        width: 100%;
    }}
    th, td {{
        border: 1px solid #45475a;
        padding: 6px 10px;
        text-align: left;
    }}
    th {{ background: #45475a; }}
    strong {{ color: #f5c2e7; }}
    em {{ color: #94e2d5; }}
    a {{ color: #89b4fa; }}
    h1, h2, h3, h4 {{ color: #cba6f7; margin: 12px 0 4px; }}
    hr {{ border: none; border-top: 1px solid #45475a; margin: 12px 0; }}
</style>
</head>
<body>{html}</body>
</html>";
    }

    private static string ConvertMarkdownToHtml(string markdown)
    {
        var text = markdown;

        // Code blocks (fenced)
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"```(\w*)\n([\s\S]*?)```",
            m => $"<pre><code>{System.Web.HttpUtility.HtmlEncode(m.Result("$2"))}</code></pre>");

        // Inline code
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"`([^`]+)`",
            m => $"<code>{System.Web.HttpUtility.HtmlEncode(m.Result("$1"))}</code>");

        // Bold
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"\*\*(.+?)\*\*", "<strong>$1</strong>");

        // Italic
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"\*(.+?)\*", "<em>$1</em>");

        // Headers
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"^#### (.+)$", "<h4>$1</h4>", System.Text.RegularExpressions.RegexOptions.Multiline);
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"^### (.+)$", "<h3>$1</h3>", System.Text.RegularExpressions.RegexOptions.Multiline);
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"^## (.+)$", "<h2>$1</h2>", System.Text.RegularExpressions.RegexOptions.Multiline);
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"^# (.+)$", "<h1>$1</h1>", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Blockquotes
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"^&gt; (.+)$", "<blockquote>$1</blockquote>", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Unordered lists
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"^- (.+)$", "<li>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"(<li>.*</li>\n?)+", "<ul>$0</ul>");

        // Ordered lists
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"^\d+\. (.+)$", "<li>$1</li>", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Horizontal rules
        text = System.Text.RegularExpressions.Regex.Replace(
            text, @"^---$", "<hr>", System.Text.RegularExpressions.RegexOptions.Multiline);

        // Paragraphs: wrap lines that aren't already HTML tags
        var lines = text.Split('\n');
        var result = new System.Text.StringBuilder();
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrEmpty(trimmed))
            {
                result.Append("<br>");
            }
            else if (trimmed.StartsWith("<"))
            {
                result.AppendLine(line);
            }
            else
            {
                result.AppendLine($"<p>{line}</p>");
            }
        }

        return result.ToString();
    }
}
