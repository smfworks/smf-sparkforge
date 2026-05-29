using System.Management;
using System.Text.RegularExpressions;
using SparkForge.Models;

namespace SparkForge.Services;

public class HardwareService
{
    public HardwareInfo Detect()
    {
        var info = new HardwareInfo();

        try
        {
            // GPU detection via WMI
            using var gpuSearcher = new ManagementObjectSearcher(
                "SELECT Name, AdapterRAM, DriverVersion FROM Win32_VideoController WHERE AdapterRAM IS NOT NULL");

            var gpus = new List<(string name, long ram, string driver)>();
            foreach (var obj in gpuSearcher.Get())
            {
                var name = obj["Name"]?.ToString() ?? "Unknown GPU";
                var ram = Convert.ToInt64(obj["AdapterRAM"] ?? 0) / (1024 * 1024);
                var driver = obj["DriverVersion"]?.ToString() ?? "";
                gpus.Add((name, ram, driver));
            }

            if (gpus.Count > 0)
            {
                var best = gpus.OrderByDescending(g => g.ram).First();
                info.GpuName = best.name;
                info.VramMb = best.ram;
                info.GpuDriver = best.driver;

                // Detect GPU vendor for acceleration hints
                var nameLower = best.name.ToLower();
                if (nameLower.Contains("nvidia") || nameLower.Contains("rtx") || nameLower.Contains("gtx"))
                {
                    info.GpuVendor = "NVIDIA";
                    info.AccelerationEngine = "CUDA (via Ollama)";
                }
                else if (nameLower.Contains("amd") || nameLower.Contains("radeon"))
                {
                    info.GpuVendor = "AMD";
                    info.AccelerationEngine = "ROCm (via Ollama)";
                }
                else if (nameLower.Contains("intel") || nameLower.Contains("arc"))
                {
                    info.GpuVendor = "Intel";
                    info.AccelerationEngine = "oneAPI (via Ollama)";
                }
                else
                {
                    info.GpuVendor = "Unknown";
                    info.AccelerationEngine = "CPU only";
                }
            }

            // System RAM
            using var ramSearcher = new ManagementObjectSearcher(
                "SELECT TotalVisibleMemorySize, FreePhysicalMemory FROM Win32_OperatingSystem");
            foreach (var obj in ramSearcher.Get())
            {
                info.SystemRamMb = Convert.ToInt64(obj["TotalVisibleMemorySize"] ?? 0) / 1024;
                info.FreeRamMb = Convert.ToInt64(obj["FreePhysicalMemory"] ?? 0) / 1024;
                break;
            }

            // CPU info
            try
            {
                using var cpuSearcher = new ManagementObjectSearcher(
                    "SELECT Name, NumberOfCores, NumberOfLogicalProcessors FROM Win32_Processor");
                foreach (var obj in cpuSearcher.Get())
                {
                    info.CpuName = obj["Name"]?.ToString()?.Trim() ?? "Unknown CPU";
                    info.CpuCores = Convert.ToInt32(obj["NumberOfCores"] ?? 0);
                    info.CpuThreads = Convert.ToInt32(obj["NumberOfLogicalProcessors"] ?? 0);
                    break;
                }
            }
            catch { }

            // NPU detection via WMI (Windows 11 24H2+)
            try
            {
                using var npuSearcher = new ManagementObjectSearcher(
                    "SELECT Name FROM Win32_PnPEntity WHERE Name LIKE '%NPU%' OR Name LIKE '%Neural%' OR Name LIKE '%QNN%' OR Name LIKE '%Hexagon%' OR Name LIKE '%Movidius%'");
                foreach (var obj in npuSearcher.Get())
                {
                    info.HasNpu = true;
                    info.NpuName = obj["Name"]?.ToString() ?? "NPU";
                    break;
                }
            }
            catch { }

            // Check if Ollama is using GPU acceleration
            info.GpuAccelerationEnabled = DetectOllamaGpuAcceleration();
        }
        catch
        {
            // WMI might not be available
        }

        // Recommend model size based on available RAM
        info.RecommendedModelSize = info.SystemRamMb switch
        {
            < 8000 => "1-3B",
            < 16000 => "3-7B",
            < 32000 => "7-14B",
            _ => "14B+"
        };

        // Generate a human-readable summary
        info.Summary = GenerateSummary(info);

        return info;
    }

    private static bool DetectOllamaGpuAcceleration()
    {
        try
        {
            // Check common Ollama GPU DLLs
            var ollamaPaths = new[]
            {
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Ollama"),
                System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Ollama"),
            };

            foreach (var basePath in ollamaPaths)
            {
                if (!System.IO.Directory.Exists(basePath)) continue;

                // Check for CUDA DLLs
                if (System.IO.Directory.Exists(System.IO.Path.Combine(basePath, "cuda_v12")) ||
                    System.IO.File.Exists(System.IO.Path.Combine(basePath, "cudart64_12.dll")) ||
                    System.IO.File.Exists(System.IO.Path.Combine(basePath, "cudart64_11.dll")))
                    return true;

                // Check for ROCm DLLs
                if (System.IO.Directory.Exists(System.IO.Path.Combine(basePath, "rocm")) ||
                    System.IO.File.Exists(System.IO.Path.Combine(basePath, "hipblas.dll")))
                    return true;
            }

            // Also check environment variable
            var ollamaHost = Environment.GetEnvironmentVariable("OLLAMA_HOST");
            return false;
        }
        catch
        {
            return false;
        }
    }

    private static string GenerateSummary(HardwareInfo info)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(info.CpuName))
            parts.Add($"{info.CpuName} ({info.CpuCores} cores, {info.CpuThreads} threads)");

        if (!string.IsNullOrEmpty(info.GpuName))
            parts.Add($"{info.GpuVendor} GPU: {info.VramMb / 1024.0:F1} GB VRAM");

        parts.Add($"{info.SystemRamMb / 1024.0:F1} GB RAM");

        if (info.HasNpu)
            parts.Add($"NPU: {info.NpuName}");

        parts.Add(info.GpuAccelerationEnabled
            ? "GPU acceleration: Enabled"
            : "GPU acceleration: Not detected (CPU-only mode)");

        return string.Join(" | ", parts);
    }
}
