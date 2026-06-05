using BlazorMonaco.Editor;
using BlazorServerCommon_NET8.Shared;
using MermaidLiveEditor.Shared;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.JSInterop;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Syncfusion.Blazor.Diagram;
using System.Xml.Linq;
using static BlazorServerCommon_NET8.Shared.SampleMermaid;
using static System.Net.Mime.MediaTypeNames;

namespace MermaidLiveEditor;

public partial class DiagramMain
{
    #region Fields

    // -----------------------------------------------------------------------------
    // Layout / Splitter
    // React equivalent: <Resizer /> + panel width state
    // -----------------------------------------------------------------------------
    private string editorPanelWidthPx = "300px";
    private bool resizing = false;
    private double startX = 0;
    private double startWidth = 300;
    private double windowWidth;

    // -----------------------------------------------------------------------------
    // Monaco Editor
    // React equivalent: mermaidText + editor ref
    // -----------------------------------------------------------------------------
    private StandaloneCodeEditor? monacoEditor;
    private string mermaidText = SampleMermaid.DEFAULT_SAMPLE;
    private SampleKey CurrentSample = SampleKey.SampleFlowchart;

    // -----------------------------------------------------------------------------
    // Diagram reference
    // React equivalent: diagramRef
    // -----------------------------------------------------------------------------
    private SfDiagramComponent? diagram;

    // -----------------------------------------------------------------------------
    // Editor State 
    // -----------------------------------------------------------------------------
    private bool livePreview = true;
    private bool pendingRender = false;
    private bool showCopyToast = false;
    private bool isEditorContentValid = true;
    private string errorMessage = string.Empty;
    private string importSummary = string.Empty;

    // -----------------------------------------------------------------------------
    // Diagram State 
    // -----------------------------------------------------------------------------
    private bool isLoading = false;
    private int totalNodeCount = 0;

    private bool isMindmapLayout = false;
    private bool isGraphExpanded = true;

    // detected diagram type: "flowchart" | "mindmap" | "sequence"
    private string detectedType = "flowchart";

    // -----------------------------------------------------------------------------
    // View / UI Options
    // React equivalent: showGrid, showRuler
    // -----------------------------------------------------------------------------
    private bool showGrid = true;
    private bool showRuler = false;

    // -----------------------------------------------------------------------------
    // Dialog / Overlay State
    // React equivalent: export dialog + error details modal
    // -----------------------------------------------------------------------------
    private bool isExportDialogOpen = false;
    private bool isDetailsModalOpen = false;

    // -----------------------------------------------------------------------------
    // Spinner / Overlay
    // React equivalent: <Spinner isVisible={isLoading} />
    // -----------------------------------------------------------------------------
    private bool showSpinner = false;

    // -----------------------------------------------------------------------------
    // Debounce handling (Live Preview)
    // React constants: DEBOUNCE_DELAY
    // -----------------------------------------------------------------------------
    private Timer? debounceTimer;
    private const int DEBOUNCE_DELAY_MS = 500;
    #endregion

    #region Splitter

    private async Task OnSplitterMouseDown(MouseEventArgs e)
    {
        resizing = true;
        startX = e.ClientX;
        if (double.TryParse(editorPanelWidthPx.Replace("px", ""), out double w))
        {
            startWidth = w;
        }
        await JS.InvokeVoidAsync("attachResizeEvents", DotNetObjectReference.Create(this));
        windowWidth = await JS.InvokeAsync<double>("getWindowWidth");
    }

    [JSInvokable]
    public void OnMouseMove(double clientX)
    {
        if (resizing)
        {
            double newWidth = startWidth + (clientX - startX);
            double maxWidth = windowWidth * 0.4; // e.g. max 40% of window
            double minWidth = 150;
            if (newWidth > maxWidth)
            {
                newWidth = maxWidth;
            }
            if (newWidth < minWidth)
            {
                newWidth = minWidth;
            }
            editorPanelWidthPx = newWidth + "px";
            InvokeAsync(StateHasChanged);
        }

    }

    [JSInvokable]
    public void OnMouseUp()
    {
        resizing = false;
    }

    #endregion

    #region Monaco Editor

    protected override void OnInitialized()
    {
        // Default theme must already be set before Monaco initializes
        ThemeService.CurrentThemeSettings = new ThemeSettings("light");
        ThemeService.SetTheme("light", ThemeService.CurrentThemeSettings);
    }

    private StandaloneEditorConstructionOptions EditorOptions(StandaloneCodeEditor _)
    {
        return new StandaloneEditorConstructionOptions
        {
            AutomaticLayout = true,
            Language = "plaintext",
            Value = mermaidText,
            ScrollBeyondLastLine = false,
            Placeholder = "Enter Mermaid syntax here...",
            WordWrap = "on",
            LineNumbers = "on",
            Minimap = new EditorMinimapOptions
            {
                Enabled = false
            },
            Scrollbar = new EditorScrollbarOptions
            {
                VerticalScrollbarSize = 5,
                HorizontalScrollbarSize = 5
            }
        };
    }

    private async Task OnEditorContentChange(ModelContentChangedEvent _)
    {
        // Cancel any existing debounce
        debounceTimer?.Dispose();

        debounceTimer = new Timer(async __ =>
        {
            await InvokeAsync(async () =>
            {
                if (monacoEditor == null) return;

                var text = await monacoEditor.GetValue() ?? string.Empty;

                // React: setMermaidText
                mermaidText = text;

                // Update detected sample dropdown immediately based on content
                var layoutInfoForEditor = GetLayoutInfo(mermaidText);
                CurrentSample = MapTypeToSampleKey(layoutInfoForEditor.Type);

                // Reset status (React behavior)
                errorMessage = string.Empty;
                importSummary = string.Empty;
                isEditorContentValid = false;

                // Empty editor handling
                if (string.IsNullOrWhiteSpace(text))
                {
                    pendingRender = false;
                    isEditorContentValid = true;
                }

                // Live preview logic
                if (livePreview)
                {
                    pendingRender = false;
                    // validation + render happens in debounce render pipeline
                    TriggerLivePreviewDebounced();
                }
                else
                {
                    // Live preview OFF → mark pending render
                    pendingRender = true;
                }

                StateHasChanged();
            });
        },
        null,
        DEBOUNCE_DELAY_MS,
        Timeout.Infinite);
    }
    public void Dispose()
    {
        ThemeService.OnThemeChanged -= StateHasChanged;
        debounceTimer?.Dispose();
    }

    #endregion

    #region Mermaid Validation

    /// <summary>
    /// Validates Mermaid syntax using mermaid.parse via JS interop.
    /// Mirrors React validateMermaid().
    /// This method ONLY updates validation state.
    /// </summary>
    private async Task<bool> ValidateMermaidAsync(string text)
    {
        // React: empty editor is valid
        if (string.IsNullOrWhiteSpace(text))
        {
            isEditorContentValid = true;
            errorMessage = string.Empty;
            return true;
        }

        try
        {
            var result = await JS.InvokeAsync<MermaidValidationResult>(
                "mermaidInterop.validate",
                text
            );

            if (!result.IsValid)
            {
                isEditorContentValid = false;
                errorMessage = string.IsNullOrEmpty(result.Message)
                    ? "Invalid Mermaid syntax"
                    : result.Message;
                return false;
            }

            // Valid syntax
            isEditorContentValid = true;
            errorMessage = string.Empty;
            return true;
        }
        catch (JSException jsEx)
        {
            // JS execution failed
            isEditorContentValid = false;
            errorMessage = jsEx.Message;
            return false;
        }
        catch (Exception ex)
        {
            isEditorContentValid = false;
            errorMessage = ex.Message;
            return false;
        }
    }

    /// <summary>
    /// DTO for JS mermaid validation result.
    /// </summary>
    private sealed class MermaidValidationResult
    {
        public bool IsValid { get; set; }
        public string? Message { get; set; }
    }

    #endregion

    #region Mermaid Layout Resolution

    /// <summary>
    /// Represents layout information detected from Mermaid text.
    /// Mirrors the object returned by React GetLayoutInfo().
    /// </summary>
    private sealed class MermaidLayoutInfo
    {
        public string Type { get; init; } = "flowchart";
        // "flowchart" | "mindmap" | "sequence"

        public LayoutOrientation? Orientation { get; init; }
        // Only applicable for flowchart
    }

    /// <summary>
    /// Maps Mermaid direction codes to Syncfusion diagram orientations.
    /// Mirrors React dirCodeToOrientation().
    /// </summary>
    private static LayoutOrientation? DirCodeToOrientation(string? code)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        return code.ToUpperInvariant() switch
        {
            "TB" or "TD" => LayoutOrientation.TopToBottom,
            "BT" => LayoutOrientation.BottomToTop,
            "LR" => LayoutOrientation.LeftToRight,
            "RL" => LayoutOrientation.RightToLeft,
            _ => null
        };
    }

    /// <summary>
    /// Detects Mermaid diagram type and orientation from raw text.
    /// Pure logic, no parsing, mirrors React GetLayoutInfo().
    /// </summary>
    private MermaidLayoutInfo GetLayoutInfo(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return new MermaidLayoutInfo
            {
                Type = "flowchart"
            };
        }

        // Find first non-empty line
        var firstLine = text
            .Split(new[] { "\r\n", "\n" }, StringSplitOptions.None)
            .FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));

        if (firstLine == null)
        {
            return new MermaidLayoutInfo
            {
                Type = "flowchart"
            };
        }

        // Remove BOM / control chars and normalize
        var normalized = firstLine.TrimStart('\uFEFF').Trim();
        var lower = normalized.ToLowerInvariant();

        // Sequence diagram
        if (lower.StartsWith("sequence"))
        {
            return new MermaidLayoutInfo
            {
                Type = "sequence"
            };
        }

        // Mindmap
        if (lower.StartsWith("mindmap"))
        {
            return new MermaidLayoutInfo
            {
                Type = "mindmap"
            };
        }

        // Flowchart / Graph
        // Matches: flowchart LR, graph TD, etc.
        var match = System.Text.RegularExpressions.Regex.Match(
            normalized,
            @"^(flowchart|graph)\b(?:\s+([A-Za-z]{2}))?",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase
        );

        if (match.Success)
        {
            var dirCode = match.Groups.Count > 2
                ? match.Groups[2].Value
                : null;

            return new MermaidLayoutInfo
            {
                Type = "flowchart",
                Orientation = DirCodeToOrientation(dirCode)
            };
        }

        // Default fallback
        return new MermaidLayoutInfo
        {
            Type = "flowchart"
        };
    }

    #endregion

    #region Diagram Component

    // ============================================================================
    // Diagram lifecycle  (React: created={() => renderFromMermaid(mermaidText)})
    // ============================================================================

    private async Task OnDiagramCreated()
    {
        if (diagram == null)
            return;

        // Empty editor → valid empty state
        if (string.IsNullOrWhiteSpace(mermaidText))
        {
            isEditorContentValid = true;
            errorMessage = string.Empty;
            importSummary = string.Empty;
            totalNodeCount = 0;
            return;
        }
        Service?.Update(new NotifyProperties() { HideSpinner = true });

        // Validate first (React parity)
        var isValid = await ValidateMermaidAsync(mermaidText);
        if (!isValid)
            return;

        // Initial render
        await RenderFromMermaidAsync(mermaidText);
    }

    // ============================================================================
    // Live Preview – debounced render
    // (React useEffect with DEBOUNCE_DELAY)
    // ============================================================================

    private void TriggerLivePreviewDebounced()
    {
        if (!livePreview)
            return;

        debounceTimer?.Dispose();

        debounceTimer = new Timer(async _ =>
        {
            await InvokeAsync(async () =>
            {
                if (diagram == null)
                    return;
                if (string.IsNullOrEmpty(mermaidText))
                {
                    diagram.Clear();
                    return;
                }

                var ok = await ValidateMermaidAsync(mermaidText);
                if (!ok)
                    return;

                await RenderFromMermaidAsync(mermaidText);
            });
        },
        null,
        DEBOUNCE_DELAY_MS,
        Timeout.Infinite);
    }

    // ============================================================================
    // Manual Render (Render button when Live Preview OFF)
    // ============================================================================

    private async Task HandleRenderClickAsync()
    {
        if (string.IsNullOrWhiteSpace(mermaidText))
        {
            isEditorContentValid = true;
            pendingRender = false;
            errorMessage = string.Empty;
            importSummary = string.Empty;
            totalNodeCount = 0;
        }

        pendingRender = false;
        isEditorContentValid = false;

        var ok = await ValidateMermaidAsync(mermaidText);
        if (!ok)
            return;

        await RenderFromMermaidAsync(mermaidText);
    }

    private async Task HandleClearClickAsync()
    {
        mermaidText = string.Empty;
        errorMessage = string.Empty;
        importSummary = string.Empty;
        isEditorContentValid = true;
        pendingRender = false;
        totalNodeCount = 0;

        // Clear Monaco editor content
        if (monacoEditor != null)
        {
            await monacoEditor.SetValue(string.Empty);
        }

        // Clear Syncfusion diagram (React: diagram.clear())
        if (diagram != null)
        {
            diagram.Clear();
        }

        StateHasChanged();
    }
    private async Task HandleCopyClickAsync()
    {
        try
        {
            await JS.InvokeVoidAsync("copyToClipboard", mermaidText);

            showCopyToast = true;
            StateHasChanged();

            // Auto-hide toast (React: setTimeout)
            _ = Task.Run(async () =>
            {
                await Task.Delay(1800);
                await InvokeAsync(() =>
                {
                    showCopyToast = false;
                    StateHasChanged();
                });
            });
        }
        catch
        {
            // Silent failure (same as React)
        }
    }
    private void onChange(Microsoft.AspNetCore.Components.ChangeEventArgs args)
    {
        livePreview = !livePreview;
        StateHasChanged();
    }
    // ============================================================================
    // Core Mermaid Rendering (React: renderFromMermaid)
    // ============================================================================

    private async Task RenderFromMermaidAsync(string text)
    {
        if (diagram == null)
            return;

        errorMessage = string.Empty;
        importSummary = string.Empty;
        isLoading = true;
        showSpinner = true;
        StateHasChanged();

        try
        {
            // Detect type + orientation (fallback logic like React)
            var layoutInfo = GetLayoutInfo(text);
            detectedType = layoutInfo.Type;
            // Keep the nav dropdown in sync with the detected layout
            CurrentSample = MapTypeToSampleKey(layoutInfo.Type);
            isMindmapLayout = detectedType == "mindmap";

            ApplyMermaidLayout(layoutInfo);
            diagram.Clear();
            await diagram.LoadDiagramFromMermaidAsync(text);

            diagram.FitToPage(new FitOptions
            {
                Mode = FitMode.Both,
                Region = DiagramRegion.Content
            });

            totalNodeCount = diagram.Nodes?.Count ?? 0;

            if (totalNodeCount == 0 && !string.IsNullOrWhiteSpace(text))
            {
                isEditorContentValid = false;
                errorMessage = "No diagram elements produced — possible invalid Mermaid syntax";
                importSummary = string.Empty;
            }
            else
            {
                isEditorContentValid = true;
                importSummary = "Diagram loaded successfully";
            }
        }
        catch (Exception ex)
        {
            isEditorContentValid = false;
            errorMessage = ex.Message;
        }
        finally
        {
            isLoading = false;
            showSpinner = false;
            pendingRender = false;
            await InvokeAsync(StateHasChanged);
        }
    }

    // ============================================================================
    // Layout application (React imperative layout assignment)
    // ============================================================================

    private void ApplyMermaidLayout(MermaidLayoutInfo layoutInfo)
    {
        if (diagram == null)
            return;

        if (layoutInfo.Type == "mindmap")
        {
            diagram.Layout = new Layout
            {
                Type = LayoutType.MindMap,
                Orientation = LayoutOrientation.Horizontal,
                HorizontalSpacing = 50,
                VerticalSpacing = 50
            };
        }
        else if (layoutInfo.Type == "flowchart")
        {
            diagram.Layout = new Layout
            {
                Type = LayoutType.Flowchart,
                Orientation = layoutInfo.Orientation ?? LayoutOrientation.TopToBottom
            };
        }
        else
        {
            // sequence diagram → no automatic layout
            diagram.Layout = null;
        }
    }

    // ============================================================================
    // Node defaults (React: getNodeDefaults)
    // ============================================================================

    private void OnNodeCreating(IDiagramObject obj)
    {
        if (obj is not Node node)
            return;

        switch (detectedType)
        {
            case "mindmap":
                node.Width ??= 140;
                node.Height ??= 36;

                CreateExpandCollapseIcon(node, true);
                CreateExpandCollapseIcon(node, false);
                break;

            case "flowchart":
                node.Width ??= 130;
                node.Height ??= 48;
                break;
        }

        node.Style ??= new ShapeStyle
        {
            Fill = ThemeService.CurrentThemeSettings.NodeFillColor,
            StrokeColor = ThemeService.CurrentThemeSettings.NodeStrokeColor,
            StrokeWidth = 1
        };
    }
    private void CreateExpandCollapseIcon(Node node, bool isExpandIcon)
    {
        if (isExpandIcon)
        {
            node.ExpandIcon = new DiagramExpandIcon()
            {
                Shape = DiagramExpandIcons.Template,
                Width = 0,
                Height = 0,
                HorizontalAlignment = HorizontalAlignment.Right,
                OffsetX = 0,
                OffsetY = 0.5
            };
        }
        else
        {
            node.CollapseIcon = new DiagramCollapseIcon()
            {
                Shape = DiagramCollapseIcons.Template,
                Width = 0,
                Height = 0,
                HorizontalAlignment = HorizontalAlignment.Right,
                OffsetX = 0,
                OffsetY = 0.5,
            };
        }
    }


    // ============================================================================
    // Connector defaults (React: getConnectorDefaults)
    // ============================================================================

    private void OnConnectorCreating(IDiagramObject obj)
    {
        if (obj is not Connector connector)
            return;
        if (detectedType != "sequence")
        {
            // Type (flowchart -> Orthogonal, mindmap -> Bezier)
            connector.Type = detectedType switch
            {
                "mindmap" => ConnectorSegmentType.Bezier,
                _ => ConnectorSegmentType.Orthogonal
            };
            connector.TargetDecorator = new DecoratorSettings
            {
                Shape = DecoratorShape.None
            };
        }
        var strokeColor = ThemeService.CurrentThemeSettings.ConnectorStrokeColor;

        // Ensure connector style and basic stroke
        connector.Style ??= new ShapeStyle();
        connector.Style.StrokeColor = strokeColor;
        connector.Style.StrokeWidth = 1.5;

        if (connector.SourceDecorator != null)
        {
            connector.SourceDecorator.Style.Fill = strokeColor;
            connector.SourceDecorator.Style.StrokeColor = strokeColor;
        }

        if (connector.TargetDecorator != null)
        {
            connector.TargetDecorator.Style.Fill = strokeColor;
            connector.TargetDecorator.Style.StrokeColor = strokeColor;
        }

        // If there are annotations, set the first annotation's fill (React sets white)
        if (connector.Annotations != null && connector.Annotations.Count > 0)
        {
            foreach (PathAnnotation annotation in connector.Annotations)
            {
                annotation.Style.Color = ThemeService.CurrentThemeSettings.TextValueColor;
            }
        }
    }
    private async Task RefreshConnectorStylesAsync()
    {
        if (diagram?.Connectors == null)
            return;

        diagram.BeginUpdate();

        foreach (var connector in diagram.Connectors)
        {
            connector.Style ??= new ShapeStyle();

            connector.Style.StrokeColor =
                ThemeService.CurrentThemeSettings.ConnectorStrokeColor;

            connector.Style.StrokeWidth = 1.5;

            if (connector.SourceDecorator != null)
            {
                connector.SourceDecorator.Style.Fill = ThemeService.CurrentThemeSettings.ConnectorStrokeColor;
                connector.SourceDecorator.Style.StrokeColor = ThemeService.CurrentThemeSettings.ConnectorStrokeColor;
            }
            if(connector.Annotations.Count > 0)
            {

                foreach (PathAnnotation annotation in connector.Annotations)
                {
                    annotation.Style.Color = ThemeService.CurrentThemeSettings.TextValueColor;
                }
            }
        }

        await diagram.EndUpdateAsync();
    }
    #endregion

    #region Popup Dialog

    // ─────────────────────────────────────────────
    // Error Details Dialog
    // ─────────────────────────────────────────────

    private void OpenErrorDetails()
    {
        if (!string.IsNullOrWhiteSpace(errorMessage))
        {
            isDetailsModalOpen = true;
        }
    }

    private void CloseErrorDetails()
    {
        isDetailsModalOpen = false;
    }

    // ─────────────────────────────────────────────
    // Export Image Dialog
    // ─────────────────────────────────────────────

    private void OpenExportDialog()
    {
        isExportDialogOpen = true;
    }

    private void CloseExportDialog()
    {
        isExportDialogOpen = false;
    }

    /// <summary>
    /// Export diagram as an image using Syncfusion Blazor Diagram API.
    /// Correct implementation (no assumed overloads).
    /// </summary>
    private async Task HandleDiagramExportAsync((string FileName, string Format) args)
    {
        if (!Enum.TryParse<DiagramExportFormat>(args.Format, true, out var exportFormat))
        {
            errorMessage = $"Unsupported export format: {args.Format}";
            return;
        }

        var exportSettings = new DiagramExportSettings() { FitToPage = true , Region = DiagramPrintExportRegion.Content};
        await diagram.ExportAsync(args.FileName, exportFormat, exportSettings);
    }


    #endregion

    #region Nav Menu Actions

    // ─────────────────────────────────────────────
    // File actions
    // ─────────────────────────────────────────────

    private async Task HandleNavBarActionAsync(string action)
    {
        switch (action)
        {
            case "new":
                await HandleNewDiagramAsync();
                break;

            case "reset":
                await HandleResetDiagramAsync();
                break;

            case "import":
                await HandleImportMermaidAsync();
                break;

            case "export-image":
                OpenExportDialog();
                break;

            case "export-code":
                ExportMermaidText();
                break;

            case "toggle-grid":
                ToggleGrid();
                break;

            case "toggle-ruler":
                ToggleRuler();
                break;

            case "theme-light":
            case "theme-dark":
                await HandleThemeChangeAsync(action.Contains("dark") ? "dark" : "light");
                break;
        }
    }
    // ─────────────────────────────────────────────
    // File handlers
    // ─────────────────────────────────────────────

    private async Task HandleNewDiagramAsync()
    {
        mermaidText = string.Empty;
        errorMessage = string.Empty;
        importSummary = string.Empty;
        isEditorContentValid = true;
        pendingRender = false;
        totalNodeCount = 0;

        // Clear Monaco editor content if available
        if (monacoEditor != null)
        {
            await monacoEditor.SetValue(string.Empty);
        }

        // Clear Syncfusion diagram if available
        if (diagram != null)
        {
            diagram.Clear();
        }

        StateHasChanged();
    }

    private async Task HandleResetDiagramAsync()
    {
        mermaidText = SampleMermaid.DEFAULT_SAMPLE;
        errorMessage = string.Empty;
        importSummary = string.Empty;
        pendingRender = false;

        await RenderFromMermaidAsync(mermaidText);
    }

    private async Task HandleImportMermaidAsync()
    {
        await JS.InvokeVoidAsync("importMermaidFile", DotNetObjectReference.Create(this));
    }

    [JSInvokable]
    public async Task ReceiveImportedMermaid(string content, string? fileName)
    {
        mermaidText = content;
        // Update detected sample so the nav dropdown reflects imported content
        var layoutFromImport = GetLayoutInfo(mermaidText);
        CurrentSample = MapTypeToSampleKey(layoutFromImport.Type);

        // Show file uploaded status immediately
        importSummary = string.IsNullOrWhiteSpace(fileName) ? "File uploaded" : $"File uploaded: {fileName}";

        // Update editor content
        if (monacoEditor != null)
        {
            await monacoEditor.SetValue(mermaidText);
        }

        pendingRender = !livePreview;

        // Validate before attempting to render so we don't clear the existing diagram on error
        var ok = await ValidateMermaidAsync(mermaidText);
        StateHasChanged();

        if (!ok)
        {
            // Keep previous diagram visible; overlay will show because isEditorContentValid == false
            return;
        }

        if (livePreview)
        {
            await RenderFromMermaidAsync(mermaidText);
        }
    }

    private SampleMermaid.SampleKey MapTypeToSampleKey(string type)
    {
        return type switch
        {
            "mindmap" => SampleMermaid.SampleKey.SampleMindmap,
            "sequence" => SampleMermaid.SampleKey.SampleSequence,
            _ => SampleMermaid.SampleKey.SampleFlowchart,
        };
    }

    private async Task HandleSampleLoadAsync(SampleMermaid.SampleKey key)
    {
        if (SampleMermaid.SAMPLES.TryGetValue(key, out var sample))
        {
            pendingRender = false;
            CurrentSample = key;
            mermaidText = SampleMermaid.SAMPLES[key];

            await monacoEditor.SetValue(mermaidText);
            await RenderFromMermaidAsync(mermaidText);
            StateHasChanged();
        }
    }

    private void ExportMermaidText()
    {
        JS.InvokeVoidAsync(
            "exportMermaidFile",
            mermaidText,
            "diagram.mmd"
        );
    }

    // ─────────────────────────────────────────────
    // View options
    // ─────────────────────────────────────────────

    private void ToggleGrid()
    {
        showGrid = !showGrid;
    }

    private void ToggleRuler()
    {
        showRuler = !showRuler;
    }

    // ─────────────────────────────────────────────
    // Theme handling
    // ─────────────────────────────────────────────

    private async Task HandleThemeChangeAsync(string theme)
    {
        if (ThemeService.CurrentTheme == theme)
            return;

        ThemeService.CurrentThemeSettings = new ThemeSettings(theme);
        ThemeService.SetTheme(theme, ThemeService.CurrentThemeSettings);

        await JS.InvokeVoidAsync(
            "setTheme",
            ThemeService.CurrentThemeSettings.CssUrl,
            ThemeService.CurrentThemeSettings.DiagramBackgroundColor,
            theme == "dark"
        );

        if (monacoEditor != null)
        {
            await monacoEditor.UpdateOptions(new EditorUpdateOptions
            {
                Theme = theme == "dark" ? "vs-dark" : "vs"
            });
        }
        await RefreshConnectorStylesAsync();
        StateHasChanged();
    }


    #endregion

    #region Hamburger Menu Actions

    // ─────────────────────────────────────────────
    // Mindmap: Expand / Collapse graph
    // Mirrors React toggleExpandGraphOnly()
    // ─────────────────────────────────────────────

    private async void ToggleExpandGraphOnly()
    {
        // React behavior: works only for mindmap
        if (!isMindmapLayout || diagram == null)
            return;

        if (diagram.Nodes == null || diagram.Nodes.Count == 0)
            return;

        var nextExpandedState = !isGraphExpanded;
        diagram.BeginUpdate();
        for(int i = 0; i < diagram.Nodes.Count; i++)
        {
            var node = diagram.Nodes[i];
            if (diagram.Nodes[i] != null)
            {
                diagram.Nodes[i].IsExpanded = nextExpandedState;
            }
        }

        await diagram.EndUpdateAsync();

        await diagram.DoLayoutAsync();

        isGraphExpanded = nextExpandedState;
    }

    #endregion

    #region Toolbar Actions

    private const double ZOOM_STEP = 1.2;

    // ─────────────────────────────────────────────
    // Entry point from Toolbar component
    // ─────────────────────────────────────────────

    private void HandleToolBarAction(string action)
    {
        if (diagram == null)
            return;

        switch (action)
        {
            case "reset":
                HandleReset();
                break;

            case "fitToPage":
                HandleFitToPage();
                break;

            case "zoomIn":
                HandleZoomIn();
                break;

            case "zoomOut":
                HandleZoomOut();
                break;
        }
    }

    // ─────────────────────────────────────────────
    // Toolbar actions (React parity, Blazor API correct)
    // ─────────────────────────────────────────────

    private void HandleReset()
    {
        // Syncfusion Blazor API
        diagram.ResetZoom();
    }

    private void HandleFitToPage()
    {
        diagram.FitToPage(new FitOptions
        {
            Mode = FitMode.Both,
            Region = DiagramRegion.Content
        });
    }

    private void HandleZoomIn()
    {
        // Zoom in by factor (1.2x), center-focused
        diagram.Zoom(ZOOM_STEP, null);
    }

    private void HandleZoomOut()
    {
        // Zoom out by inverse factor, center-focused
        diagram.Zoom(1 / ZOOM_STEP, null);
    }

    #endregion
}