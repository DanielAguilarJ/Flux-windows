using System;
using System.Collections.Generic;
using System.Management;
using System.Threading.Tasks;

/// <summary>
/// Prueba independiente del sistema de diagnóstico mejorado
/// </summary>
public class DiagnosticTest
{
    public static async Task Main(string[] args)
    {
        Console.WriteLine("=== PRUEBA DE DIAGNÓSTICO MEJORADO ===\n");
        
        try
        {
            var systemInfo = await GetDetailedSystemInfoAsync();
            
            Console.WriteLine("📊 INFORMACIÓN DETALLADA DEL SISTEMA:");
            foreach (var info in systemInfo)
            {
                Console.WriteLine($"  {info}");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error durante la prueba: {ex.Message}");
        }

        Console.WriteLine("\nPresiona cualquier tecla para continuar...");
        Console.ReadKey();
    }

    private static async Task<List<string>> GetDetailedSystemInfoAsync()
    {
        return await Task.Run(() =>
        {
            var systemInfo = new List<string>();
            
            try
            {
                // Operating System Information
                var osInfo = Environment.OSVersion;
                var osArchitecture = Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit";
                systemInfo.Add($"💻 Sistema: {osInfo.VersionString} ({osArchitecture})");
                
                // .NET Runtime Information
                var runtimeVersion = Environment.Version;
                systemInfo.Add($"🔧 .NET Runtime: {runtimeVersion}");
                
                // Processor Information
                var processorName = "Desconocido";
                var processorCores = Environment.ProcessorCount;
                
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Name FROM Win32_Processor"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            processorName = obj["Name"]?.ToString()?.Trim() ?? "Desconocido";
                            break; // Get first processor
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Debug: Could not retrieve processor info: {ex.Message}");
                    processorName = "No disponible";
                }
                
                systemInfo.Add($"⚙️ Procesador: {processorName} ({processorCores} núcleos)");
                
                // Memory Information
                var totalMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                systemInfo.Add($"💾 Memoria en uso por .NET: ~{totalMemoryMB} MB");
                
                // Get total system memory
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            if (obj["TotalPhysicalMemory"] != null)
                            {
                                var totalMemoryBytes = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                                var totalMemoryGB = Math.Round(totalMemoryBytes / (1024.0 * 1024.0 * 1024.0), 1);
                                systemInfo.Add($"🧠 Memoria total del sistema: {totalMemoryGB} GB");
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Debug: Could not retrieve total memory: {ex.Message}");
                    systemInfo.Add("🧠 Memoria total: No disponible");
                }
                
                // Graphics Card Information
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT Name, AdapterRAM FROM Win32_VideoController"))
                    {
                        var gpuIndex = 1;
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            var gpuName = obj["Name"]?.ToString();
                            if (!string.IsNullOrEmpty(gpuName) && !gpuName.Contains("Generic") && !gpuName.Contains("Basic"))
                            {
                                var gpuRamBytes = obj["AdapterRAM"];
                                if (gpuRamBytes != null && gpuRamBytes.ToString() != "0")
                                {
                                    var gpuRamGB = Math.Round(Convert.ToUInt64(gpuRamBytes) / (1024.0 * 1024.0 * 1024.0), 1);
                                    systemInfo.Add($"🎮 GPU {gpuIndex}: {gpuName} ({gpuRamGB} GB VRAM)");
                                }
                                else
                                {
                                    systemInfo.Add($"🎮 GPU {gpuIndex}: {gpuName}");
                                }
                                gpuIndex++;
                            }
                        }
                        
                        if (gpuIndex == 1)
                        {
                            systemInfo.Add("🎮 GPU: No detectado o información no disponible");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Debug: Could not retrieve GPU info: {ex.Message}");
                    systemInfo.Add("🎮 GPU: No disponible");
                }
                
                // Windows Version Details
                try
                {
                    var windowsVersion = GetWindowsVersion();
                    if (!string.IsNullOrEmpty(windowsVersion))
                    {
                        systemInfo.Add($"🪟 Versión Windows: {windowsVersion}");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Debug: Could not retrieve Windows version: {ex.Message}");
                }
                
                // Power Plan Information (affects gamma ramp performance)
                try
                {
                    using (var searcher = new ManagementObjectSearcher("root\\cimv2\\power", "SELECT ElementName, IsActive FROM Win32_PowerPlan"))
                    {
                        foreach (ManagementObject obj in searcher.Get())
                        {
                            var isActive = obj["IsActive"];
                            if (isActive != null && (bool)isActive)
                            {
                                var powerPlan = obj["ElementName"]?.ToString() ?? "Desconocido";
                                systemInfo.Add($"🔋 Plan de energía activo: {powerPlan}");
                                break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Debug: Could not retrieve power plan: {ex.Message}");
                }
                
                // Application-specific information
                var processArchitecture = Environment.Is64BitProcess ? "64-bit" : "32-bit";
                systemInfo.Add($"🏗️ Proceso: {processArchitecture}");
                
                // Machine name and user info
                systemInfo.Add($"🖥️ Nombre del equipo: {Environment.MachineName}");
                systemInfo.Add($"👤 Usuario: {Environment.UserName}");
                systemInfo.Add($"🌐 Dominio: {Environment.UserDomainName}");
                
            }
            catch (Exception ex)
            {
                systemInfo.Add($"❌ Error obteniendo información del sistema: {ex.Message}");
            }
            
            return systemInfo;
        });
    }
    
    private static string GetWindowsVersion()
    {
        try
        {
            using (var searcher = new ManagementObjectSearcher("SELECT Caption, Version, BuildNumber FROM Win32_OperatingSystem"))
            {
                foreach (ManagementObject obj in searcher.Get())
                {
                    var caption = obj["Caption"]?.ToString() ?? "";
                    var version = obj["Version"]?.ToString() ?? "";
                    var buildNumber = obj["BuildNumber"]?.ToString() ?? "";
                    
                    if (!string.IsNullOrEmpty(caption))
                    {
                        if (!string.IsNullOrEmpty(buildNumber))
                        {
                            return $"{caption} (Build {buildNumber})";
                        }
                        return caption;
                    }
                    break;
                }
            }
        }
        catch
        {
            // Fallback to basic version if WMI fails
        }
        
        return Environment.OSVersion.ToString();
    }
}
