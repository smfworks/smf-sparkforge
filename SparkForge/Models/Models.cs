namespace SparkForge.Models;

public class Conversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "New Chat";
    public string Model { get; set; } = "";
    public string SystemPrompt { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class ChatMessage
{
    public long Id { get; set; }
    public string ConversationId { get; set; } = "";
    public string Role { get; set; } = ""; // "user", "assistant", "system"
    public string Content { get; set; } = "";
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class OllamaModel
{
    public string Name { get; set; } = "";
    public long Size { get; set; }
    public string ModifiedAt { get; set; } = "";
    public string DisplayName => Name;
    public string SizeFormatted => Size >= 1_000_000_000
        ? $"{Size / 1_000_000_000.0:F1} GB"
        : $"{Size / 1_000_000.0:F0} MB";
}

public class ModelInfo
{
    public string Name { get; set; } = "";
    public long SizeBytes { get; set; }
    public string ParameterSize { get; set; } = "";
    public string Family { get; set; } = "";
    public string Format { get; set; } = "";
}

public class HardwareInfo
{
    public string GpuName { get; set; } = "None detected";
    public string GpuVendor { get; set; } = "";
    public string GpuDriver { get; set; } = "";
    public long VramMb { get; set; }
    public long SystemRamMb { get; set; }
    public long FreeRamMb { get; set; }
    public string CpuName { get; set; } = "";
    public int CpuCores { get; set; }
    public int CpuThreads { get; set; }
    public bool HasNpu { get; set; }
    public string NpuName { get; set; } = "";
    public string AccelerationEngine { get; set; } = "CPU only";
    public bool GpuAccelerationEnabled { get; set; }
    public string RecommendedModelSize { get; set; } = "3B";
    public string Summary { get; set; } = "";
}
