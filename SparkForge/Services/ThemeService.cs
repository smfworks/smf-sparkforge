namespace SparkForge.Services;

/// <summary>
/// Catppuccin Mocha (dark) and Latte (light) theme definitions.
/// </summary>
public static class ThemeService
{
    public enum Theme { Dark, Light }

    public static Theme CurrentTheme { get; private set; } = Theme.Dark;

    public static event Action<Theme>? ThemeChanged;

    // Catppuccin Mocha (dark)
    public static class Dark
    {
        public const string Base = "#1e1e2e";
        public const string Mantle = "#181825";
        public const string Crust = "#11111b";
        public const string Surface0 = "#313244";
        public const string Surface1 = "#45475a";
        public const string Surface2 = "#585b70";
        public const string Overlay0 = "#6c7086";
        public const string Overlay1 = "#7f849c";
        public const string Subtext0 = "#a6adc8";
        public const string Subtext1 = "#bac2de";
        public const string Text = "#cdd6f4";
        public const string Blue = "#89b4fa";
        public const string Lavender = "#b4befe";
        public const string Green = "#a6e3a1";
        public const string Red = "#f38ba8";
        public const string Peach = "#fab387";
        public const string Yellow = "#f9e2af";
        public const string Teal = "#94e2d5";
        public const string Pink = "#f5c2e7";
        public const string Mauve = "#cba6f7";
        public const string Maroon = "#eba0ac";
        public const string Border = "#313244";
    }

    // Catppuccin Latte (light)
    public static class Light
    {
        public const string Base = "#eff1f5";
        public const string Mantle = "#e6e9ef";
        public const string Crust = "#dce0e8";
        public const string Surface0 = "#ccd0da";
        public const string Surface1 = "#bcc0cc";
        public const string Surface2 = "#acb0be";
        public const string Overlay0 = "#9ca0b0";
        public const string Overlay1 = "#8c8fa1";
        public const string Subtext0 = "#6c6f85";
        public const string Subtext1 = "#5c5f77";
        public const string Text = "#4c4f69";
        public const string Blue = "#1e66f5";
        public const string Lavender = "#7287fd";
        public const string Green = "#40a02b";
        public const string Red = "#d20f39";
        public const string Peach = "#fe640b";
        public const string Yellow = "#df8e1d";
        public const string Teal = "#179299";
        public const string Pink = "#ea76cb";
        public const string Mauve = "#8839ef";
        public const string Maroon = "#e64553";
        public const string Border = "#ccd0da";
    }

    // Current theme accessors
    public static string Base => CurrentTheme == Theme.Dark ? Dark.Base : Light.Base;
    public static string Mantle => CurrentTheme == Theme.Dark ? Dark.Mantle : Light.Mantle;
    public static string Crust => CurrentTheme == Theme.Dark ? Dark.Crust : Light.Crust;
    public static string Surface0 => CurrentTheme == Theme.Dark ? Dark.Surface0 : Light.Surface0;
    public static string Surface1 => CurrentTheme == Theme.Dark ? Dark.Surface1 : Light.Surface1;
    public static string Surface2 => CurrentTheme == Theme.Dark ? Dark.Surface2 : Light.Surface2;
    public static string Overlay0 => CurrentTheme == Theme.Dark ? Dark.Overlay0 : Light.Overlay0;
    public static string Overlay1 => CurrentTheme == Theme.Dark ? Dark.Overlay1 : Light.Overlay1;
    public static string Subtext0 => CurrentTheme == Theme.Dark ? Dark.Subtext0 : Light.Subtext0;
    public static string Subtext1 => CurrentTheme == Theme.Dark ? Dark.Subtext1 : Light.Subtext1;
    public static string Text => CurrentTheme == Theme.Dark ? Dark.Text : Light.Text;
    public static string Blue => CurrentTheme == Theme.Dark ? Dark.Blue : Light.Blue;
    public static string Lavender => CurrentTheme == Theme.Dark ? Dark.Lavender : Light.Lavender;
    public static string Green => CurrentTheme == Theme.Dark ? Dark.Green : Light.Green;
    public static string Red => CurrentTheme == Theme.Dark ? Dark.Red : Light.Red;
    public static string Peach => CurrentTheme == Theme.Dark ? Dark.Peach : Light.Peach;
    public static string Yellow => CurrentTheme == Theme.Dark ? Dark.Yellow : Light.Yellow;
    public static string Teal => CurrentTheme == Theme.Dark ? Dark.Teal : Light.Teal;
    public static string Pink => CurrentTheme == Theme.Dark ? Dark.Pink : Light.Pink;
    public static string Mauve => CurrentTheme == Theme.Dark ? Dark.Mauve : Light.Mauve;
    public static string Maroon => CurrentTheme == Theme.Dark ? Dark.Maroon : Light.Maroon;
    public static string Border => CurrentTheme == Theme.Dark ? Dark.Border : Light.Border;

    public static void SetTheme(Theme theme)
    {
        if (CurrentTheme == theme) return;
        CurrentTheme = theme;
        ThemeChanged?.Invoke(theme);
    }

    public static void Toggle()
    {
        SetTheme(CurrentTheme == Theme.Dark ? Theme.Light : Theme.Dark);
    }

    /// <summary>
    /// Generates the CSS for the current theme, used in WebView2 chat rendering.
    /// </summary>
    public static string GetChatCss()
    {
        return $@"
            * {{ margin: 0; padding: 0; box-sizing: border-box; }}
            body {{ 
                background: {Base}; 
                color: {Text}; 
                font-family: 'Segoe UI', -apple-system, sans-serif;
                font-size: 13px; 
                line-height: 1.6;
                padding: 16px;
            }}
            .msg {{ margin-bottom: 12px; display: flex; }}
            .msg.user {{ justify-content: flex-end; }}
            .msg.assistant {{ justify-content: flex-start; }}
            .bubble {{
                max-width: 700px;
                padding: 12px 14px;
                border-radius: 10px;
                word-wrap: break-word;
                overflow-wrap: break-word;
            }}
            .user .bubble {{ background: {Surface1}; }}
            .assistant .bubble {{ background: {Surface0}; }}
            .role-label {{ font-size: 10px; font-weight: bold; color: {Blue}; margin-bottom: 4px; }}
            pre {{
                background: {Mantle};
                padding: 12px;
                border-radius: 6px;
                overflow-x: auto;
                margin: 8px 0;
                border: 1px solid {Surface1};
            }}
            code {{
                font-family: 'Cascadia Code', 'Fira Code', 'Consolas', monospace;
                font-size: 12px;
                background: {Mantle};
                padding: 2px 5px;
                border-radius: 3px;
            }}
            pre code {{ background: none; padding: 0; }}
            p {{ margin: 4px 0; }}
            ul, ol {{ margin: 4px 0; padding-left: 20px; }}
            li {{ margin: 2px 0; }}
            blockquote {{
                border-left: 3px solid {Blue};
                padding-left: 12px;
                margin: 8px 0;
                color: {Subtext0};
            }}
            table {{ border-collapse: collapse; margin: 8px 0; width: 100%; }}
            th, td {{ border: 1px solid {Surface1}; padding: 6px 10px; text-align: left; }}
            th {{ background: {Surface1}; }}
            strong {{ color: {Pink}; }}
            em {{ color: {Teal}; }}
            a {{ color: {Blue}; }}
            h1, h2, h3, h4 {{ color: {Mauve}; margin: 12px 0 4px; }}
            hr {{ border: none; border-top: 1px solid {Surface1}; margin: 12px 0; }}
        ";
    }
}
