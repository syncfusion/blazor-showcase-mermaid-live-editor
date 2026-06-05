namespace MermaidLiveEditor;

public class ThemeService
{
    public event Action OnThemeChanged;

    public string CurrentTheme { get; private set; } = "light"; // "light" or "dark"

    public ThemeSettings CurrentThemeSettings { get; set; }

    public void SetTheme(string theme, ThemeSettings themeSettings)
    {
        CurrentTheme = theme;
        CurrentThemeSettings = themeSettings;
        OnThemeChanged?.Invoke();
    }
}
public class ThemeSettings
{
    public string CssUrl { get; set; }
    public string DiagramBackgroundColor { get; set; }
    public string GridlinesColor { get; set; }
    public string NodeFillColor { get; set; }
    public string NodeStrokeColor { get; set; }
    public string TextKeyColor { get; set; }
    public string TextValueColor { get; set; }
    public string TextValueNullColor { get; set; }
    public string ExpandIconFillColor { get; set; }
    public string ExpandIconColor { get; set; }
    public string ExpandIconBorder { get; set; }
    public string ConnectorStrokeColor { get; set; }
    public string ChildCountColor { get; set; }
    public string BooleanColor { get; set; }
    public string NumericColor { get; set; }
    public string PopupKeyColor { get; set; }
    public string PopupValueColor { get; set; }
    public string PopupContentBGColor { get; set; }
    public string HighlightFillColor { get; set; }
    public string HighlightFocusColor { get; set; }
    public string HighlightStrokeColor { get; set; }

    public ThemeSettings(string theme = "light")
    {
        if (theme == "light")
        {
            CssUrl = "https://cdn.syncfusion.com/blazor/30.1.38/styles/tailwind.css";
            DiagramBackgroundColor = "#F8F9FA";
            GridlinesColor = "#EBE8E8";
            NodeFillColor = "rgb(255, 255, 255)";
            NodeStrokeColor = "rgb(188, 190, 192)";
            TextKeyColor = "#A020F0";
            TextValueColor = "black";
            TextValueNullColor = "rgb(41, 41, 41)";
            ExpandIconFillColor = "#e0dede";
            ExpandIconColor = "rgb(46, 51, 56)";
            ExpandIconBorder = "rgb(188, 190, 192)";
            ConnectorStrokeColor = "black";
            ChildCountColor = "rgb(41, 41, 41)";
            BooleanColor = "rgb(74, 145, 67)";
            NumericColor = "rgb(182, 60, 30)";
            PopupKeyColor = "#5C940D";
            PopupValueColor = "#1864AB";
            PopupContentBGColor = "#f0f0f0";
            HighlightFillColor = "rgba(27, 255, 0, 0.1)";
            HighlightFocusColor = "rgba(252, 255, 166, 0.57)";
            HighlightStrokeColor = "rgb(0, 135, 54)";
        }
        else
        {
            CssUrl = "https://cdn.syncfusion.com/blazor/30.1.38/styles/tailwind-dark.css";
            DiagramBackgroundColor = "#1e1e1e";
            GridlinesColor = "rgb(45, 45, 45)";
            NodeFillColor = "rgb(41, 41, 41)";
            NodeStrokeColor = "rgb(66, 66, 66)";
            TextKeyColor = "#4dabf7";
            TextValueColor = "white";
            TextValueNullColor = "rgb(151, 150, 149)";
            ExpandIconFillColor = "#1e1e1e";
            ExpandIconColor = "rgb(220, 221, 222)";
            ExpandIconBorder = "rgb(66, 66, 66)";
            ConnectorStrokeColor = "white";
            ChildCountColor = "rgb(255, 255, 255)";
            BooleanColor = "rgb(61, 226, 49)";
            NumericColor = "rgb(232, 196, 121)";
            PopupKeyColor = "#A5D8FF";
            PopupValueColor = "#40C057";
            PopupContentBGColor = "#01000c57";
            HighlightFillColor = "rgba(27, 255, 0, 0.1)";
            HighlightFocusColor = "rgba(82, 102, 0, 0.61)";
            HighlightStrokeColor = "rgb(0, 135, 54)";
        }
    }
}


