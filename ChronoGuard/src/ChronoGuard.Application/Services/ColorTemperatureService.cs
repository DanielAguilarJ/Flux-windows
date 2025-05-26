using ChronoGuard.Domain.Interfaces;
using ChronoGuard.Domain.Entities;

namespace ChronoGuard.Application.Services
{
    public class ColorTemperatureService : IColorTemperatureService
    {
        public async Task<bool> ApplyTemperatureAsync(int kelvin)
        {
            // TODO: Implementar lógica SetDeviceGammaRamp/ICC
            return await Task.FromResult(true);
        }

        public async Task<ColorTransition> CreateTransitionAsync(int fromK, int toK, TimeSpan duration)
        {
            // TODO: Implementar curva sigmoidal
            return await Task.FromResult(new ColorTransition(fromK, toK, duration));
        }
    }

    public class ProfileService : IProfileService
    {
        public async Task<IEnumerable<ColorProfile>> GetProfilesAsync()
        {
            // TODO: Leer perfiles de disco
            return await Task.FromResult(new List<ColorProfile>());
        }
        public async Task SaveProfileAsync(ColorProfile profile)
        {
            // TODO: Guardar perfil en disco
            await Task.CompletedTask;
        }
        public async Task<ColorProfile> GetActiveProfileAsync()
        {
            // TODO: Obtener perfil activo
            return await Task.FromResult<ColorProfile>(null);
        }
    }
}
