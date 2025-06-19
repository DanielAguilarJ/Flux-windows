using System;
using System.Threading.Tasks;
using ChronoGuard.Diagnostics;

namespace ChronoGuard.TestApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Console.WriteLine("ChronoGuard - Herramientas de Diagnóstico");
            Console.WriteLine("=========================================");
            Console.WriteLine();

            if (args.Length > 0 && args[0].ToLower() == "simple")
            {
                // Run simple color test
                var simpleTest = new SimpleColorTest();
                simpleTest.RunDiagnostics();
            }
            else
            {
                // Run comprehensive diagnostics
                var diagnostics = new ColorTemperatureDiagnostics();
                var result = await diagnostics.RunComprehensiveDiagnosticsAsync();
                
                Console.WriteLine("=== RESULTADOS DEL DIAGNÓSTICO ===");
                Console.WriteLine($"Compatible con hardware: {result.IsHardwareSupported}");
                Console.WriteLine($"Permisos suficientes: {result.HasSufficientPermissions}");
                Console.WriteLine($"Software conflictivo detectado: {result.HasConflictingSoftware}");
                Console.WriteLine($"Monitores compatibles: {result.SupportedMonitorCount}/{result.TotalMonitorCount}");
                
                if (result.Recommendations.Count > 0)
                {
                    Console.WriteLine("\n=== RECOMENDACIONES ===");
                    foreach (var recommendation in result.Recommendations)
                    {
                        Console.WriteLine($"• {recommendation}");
                    }
                }
                
                if (result.ErrorDetails.Count > 0)
                {
                    Console.WriteLine("\n=== DETALLES DE ERRORES ===");
                    foreach (var error in result.ErrorDetails)
                    {
                        Console.WriteLine($"• {error}");
                    }
                }
                
                Console.WriteLine($"\nDiagnóstico completado en {result.ExecutionTime.TotalSeconds:F1} segundos");
            }

            Console.WriteLine("\nPresiona cualquier tecla para salir...");
            Console.ReadKey();
        }
    }
}
