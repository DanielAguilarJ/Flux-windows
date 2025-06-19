using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Threading;

namespace ChronoGuard.SystemTrayTest;

/// <summary>
/// Simple test program to verify ChronoGuard system tray and single-instance functionality
/// </summary>
class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("=== ChronoGuard System Tray & Lifecycle Test ===\n");
        
        // Test 1: Check if ChronoGuard is running
        Console.WriteLine("1. Checking if ChronoGuard is currently running...");
        var processes = Process.GetProcessesByName("ChronoGuard.App");
        if (processes.Length > 0)
        {
            Console.WriteLine($"✅ Found {processes.Length} ChronoGuard process(es) running:");
            foreach (var process in processes)
            {
                Console.WriteLine($"   - PID: {process.Id}, Memory: {process.WorkingSet64 / 1024 / 1024:F1} MB");
            }
        }
        else
        {
            Console.WriteLine("❌ No ChronoGuard processes found");
        }
        
        Console.WriteLine();
        
        // Test 2: Try to start another instance (should be blocked by Mutex)
        Console.WriteLine("2. Testing single-instance enforcement...");
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project \"c:\\Users\\junco\\OneDrive\\JUNCOKEVIN\\Documents\\GitHub\\Flux-windows\\ChronoGuard\\src\\ChronoGuard.App\"",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };
            
            using var testProcess = Process.Start(startInfo);
            if (testProcess != null)
            {
                await testProcess.WaitForExitAsync();
                Console.WriteLine($"✅ Second instance exited with code: {testProcess.ExitCode}");
                if (testProcess.ExitCode == 0)
                {
                    Console.WriteLine("   Single-instance enforcement is working correctly!");
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"❌ Error testing second instance: {ex.Message}");
        }
        
        Console.WriteLine();
        
        // Test 3: Check system tray functionality
        Console.WriteLine("3. System Tray Functionality Test:");
        Console.WriteLine("   - Look for ChronoGuard icon in system tray");
        Console.WriteLine("   - Right-click the icon to test context menu");
        Console.WriteLine("   - Test pause/resume functionality");
        Console.WriteLine("   - Test profile switching");
        Console.WriteLine("   - Test 'Show Main Window' option");
        
        Console.WriteLine();
        
        // Test 4: Check application lifecycle events
        Console.WriteLine("4. Application Lifecycle Events:");
        Console.WriteLine("   - Lock your computer (Win+L) to test session change detection");
        Console.WriteLine("   - Put computer to sleep to test power state changes");
        Console.WriteLine("   - Check if the app responds appropriately");
        
        Console.WriteLine();
        Console.WriteLine("Press any key to exit...");
        Console.ReadKey();
    }
}
