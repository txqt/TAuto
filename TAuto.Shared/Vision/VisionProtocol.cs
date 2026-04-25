namespace TAuto.Shared.Vision;

/// <summary>
/// IPC protocol messages for VisionServer communication.
/// These DTOs are serializable to JSON for pipe transport.
/// Prefixed with "VisionIpc" to avoid collision with AutoBot.Platform.Vision.VisionRequest.
/// </summary>

public class VisionIpcRequest
{
    public string RequestId { get; set; } = Guid.NewGuid().ToString("N");
    public VisionIpcRequestType Type { get; set; }
    
    // FindTemplate params
    public byte[]? SourceImageData { get; set; }
    public int SourceWidth { get; set; }
    public int SourceHeight { get; set; }
    public byte[]? TemplateImageData { get; set; }
    public int TemplateWidth { get; set; }
    public int TemplateHeight { get; set; }
    public double Threshold { get; set; }
    public string? TemplatePath { get; set; }
    public VisionRectDto? Roi { get; set; }
    public bool DisableMultiScale { get; set; }
    
    // FindTemplates params (batch)
    public VisionIpcTemplateBatch[]? TemplateBatch { get; set; }
    public VisionRectDto[]? Regions { get; set; }
    public bool FallbackFullscreen { get; set; }
    
    // OCR params
    public string Language { get; set; } = "eng";
    public double Scale { get; set; } = 1.0;
    public string? Whitelist { get; set; }
    public int OcrThreshold { get; set; }
    public bool Invert { get; set; }
    public int BorderSize { get; set; }
    public int PageSegMode { get; set; } = 3;
    
    // Color detection params
    public int TargetColorArgb { get; set; }
    public int ColorTolerance { get; set; } = 10;
    public VisionRectDto? SearchRegion { get; set; }
    public int MinPixelCount { get; set; } = 1;

    // Template repository
    public string? FilePath { get; set; }
    public string? BaseDirectory { get; set; }
    public string? TemplateName { get; set; }
    public string[]? PreloadPaths { get; set; }
}

public class VisionIpcTemplateBatch
{
    public byte[]? TemplateData { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public string? Path { get; set; }
    public double Threshold { get; set; }
}

public enum VisionIpcRequestType
{
    FindTemplate,
    FindTemplates,
    GetText,
    GetTextBlocks,
    GetTextWithVoting,
    GetTextWithSegmentation,
    FindColor,
    LoadTemplate,
    SaveTemplate,
    GetSavedTemplates,
    DeleteTemplate,
    PreloadTemplates,
    Shutdown
}

public class VisionIpcResponse
{
    public string RequestId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    
    // Template match result
    public VisionMatchResultDto? MatchResult { get; set; }
    public VisionMatchResultDto[]? MatchResults { get; set; }
    
    // OCR result
    public string? Text { get; set; }
    public VisionOcrBlockDto[]? TextBlocks { get; set; }
    
    // Color result
    public VisionColorResultDto? ColorResult { get; set; }
    
    // Template repository
    public byte[]? ImageData { get; set; }
    public int ImageWidth { get; set; }
    public int ImageHeight { get; set; }
    public string? SavedPath { get; set; }
    public string[]? TemplatePaths { get; set; }
    public bool DeleteResult { get; set; }
}

public class VisionMatchResultDto
{
    public bool Found { get; set; }
    public double Confidence { get; set; }
    public int LocationX { get; set; }
    public int LocationY { get; set; }
    public int CenterX { get; set; }
    public int CenterY { get; set; }
    public int TemplateWidth { get; set; }
    public int TemplateHeight { get; set; }
    public string? ErrorMessage { get; set; }
}

public class VisionOcrBlockDto
{
    public string Text { get; set; } = string.Empty;
    public double Confidence { get; set; }
    public int RectX { get; set; }
    public int RectY { get; set; }
    public int RectWidth { get; set; }
    public int RectHeight { get; set; }
}

public class VisionColorResultDto
{
    public bool Found { get; set; }
    public int? CenterX { get; set; }
    public int? CenterY { get; set; }
    public int? BoundsX { get; set; }
    public int? BoundsY { get; set; }
    public int? BoundsWidth { get; set; }
    public int? BoundsHeight { get; set; }
    public int PixelCount { get; set; }
    public double MatchPercent { get; set; }
}

/// <summary>
/// Flat rectangle DTO for IPC serialization (avoids System.Drawing dependency in Shared).
/// </summary>
public class VisionRectDto
{
    public int X { get; set; }
    public int Y { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
}
