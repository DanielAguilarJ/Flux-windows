using System;
using System.Collections.Generic;
using System.Management;
using System.Threading.Tasks;

namespace ChronoGuard.DiagnosticsTest
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            Console.WriteLine("=== PRUEBA DE DIAGNÓSTICO DE CHRONOGUARD ===");
            Console.WriteLine("Obteniendo información detallada del sistema...\n");

            var systemInfo = await GetDetailedSystemInfoAsync();
            
            Console.WriteLine("📊 INFORMACIÓN DETALLADA DEL SISTEMA:");
            Console.WriteLine("====================================");
            foreach (var info in systemInfo)
            {
                Console.WriteLine($"  {info}");
            }

            Console.WriteLine("\nℹ️  El diagnóstico ha sido completado exitosamente.");
            Console.WriteLine("Esta información será incluida en reportes de diagnóstico de ChronoGuard.");
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
                    
                    // Machine Name and User
                    systemInfo.Add($"🖥️  Nombre del equipo: {Environment.MachineName}");
                    systemInfo.Add($"👤 Usuario: {Environment.UserName}");
                    
                    // Processor Information
                    string processorName = "Desconocido";
                    int processorCores = Environment.ProcessorCount;
                    
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
                        Console.WriteLine($"⚠️  Advertencia: No se pudo obtener información del procesador: {ex.Message}");
                        processorName = "No disponible (acceso limitado)";
                    }
                    
                    systemInfo.Add($"⚙️  Procesador: {processorName} ({processorCores} núcleos)");
                    
                    // Memory Information
                    var usedMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);
                    systemInfo.Add($"💾 Memoria en uso por .NET: ~{usedMemoryMB} MB");
                    
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
                        Console.WriteLine($"⚠️  Advertencia: No se pudo obtener información de memoria total: {ex.Message}");
                        systemInfo.Add("🧠 Memoria total: No disponible (acceso limitado)");
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
                        Console.WriteLine($"⚠️  Advertencia: No se pudo obtener información de GPU: {ex.Message}");
                        systemInfo.Add("🎮 GPU: No disponible (acceso limitado)");
                    }
                    
                    // Display Information
                    try
                    {
                        using (var searcher = new ManagementObjectSearcher("SELECT CurrentHorizontalResolution, CurrentVerticalResolution, CurrentRefreshRate FROM Win32_VideoController"))
                        {
                            var displayIndex = 1;
                            foreach (ManagementObject obj in searcher.Get())
                            {
                                var width = obj["CurrentHorizontalResolution"];
                                var height = obj["CurrentVerticalResolution"];
                                var refreshRate = obj["CurrentRefreshRate"];
                                
                                if (width != null && height != null && width.ToString() != "0" && height.ToString() != "0")
                                {
                                    var resolution = $"{width}x{height}";
                                    if (refreshRate != null && refreshRate.ToString() != "0")
                                    {
                                        resolution += $" @ {refreshRate}Hz";
                                    }
                                    systemInfo.Add($"🖥️  Pantalla {displayIndex}: {resolution}");
                                    displayIndex++;
                                }
                            }
                            
                            if (displayIndex == 1)
                            {
                                // Fallback information
                                systemInfo.Add($"🖥️  Pantalla: Información no disponible via WMI");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️  Advertencia: No se pudo obtener información de pantalla: {ex.Message}");
                        systemInfo.Add("🖥️  Pantalla: No disponible (acceso limitado)");
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
                        Console.WriteLine($"⚠️  Advertencia: No se pudo obtener versión de Windows: {ex.Message}");
                    }
                    
                    // Power Plan Information (important for gamma ramp performance)
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
                        Console.WriteLine($"⚠️  Advertencia: No se pudo obtener plan de energía: {ex.Message}");
                        systemInfo.Add("🔋 Plan de energía: No disponible (acceso limitado)");
                    }
                    
                    // Application-specific information
                    var appVersion = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";
                    systemInfo.Add($"📱 Versión de prueba: v{appVersion}");
                    
                    var processArchitecture = Environment.Is64BitProcess ? "64-bit" : "32-bit";
                    systemInfo.Add($"🏗️  Arquitectura del proceso: {processArchitecture}");
                    
                    // Current user privileges
                    var isAdmin = IsRunningAsAdministrator();
                    var userContext = isAdmin ? "Administrador" : "Usuario estándar";
                    systemInfo.Add($"👤 Contexto de usuario: {userContext}");
                    
                    // System uptime
                    var uptime = TimeSpan.FromMilliseconds(Environment.TickCount);
                    systemInfo.Add($"⏱️  Tiempo de actividad del sistema: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m");
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error general obteniendo información del sistema: {ex.Message}");
                    systemInfo.Add($"❌ Error general: {ex.Message}");
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
        
        private static bool IsRunningAsAdministrator()
        {
            try
            {
                var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
