using ChronoGuard.Domain.Interfaces;
using ChronoGuard.Domain.Entities;
using System.Net.Http.Json;

namespace ChronoGuard.Infrastructure.Services
{
    public class IpLocationService : ILocationService
    {
        private const string API_URL = "https://ipapi.co/json/";
        private readonly HttpClient _httpClient = new();
        public async Task<Location> GetCurrentLocationAsync()
        {
            var response = await _httpClient.GetAsync(API_URL);
            var data = await response.Content.ReadFromJsonAsync<IpApiResponse>();
            return new Location(data.Latitude, data.Longitude, data.City);
        }
        public async Task<SolarTimes> CalculateSolarTimesAsync(Location location, DateTime date)
        {
            // TODO: Implementar cálculo solar
            return await Task.FromResult(new SolarTimes());
        }
        private class IpApiResponse
        {
            public double Latitude { get; set; }
            public double Longitude { get; set; }
            public string City { get; set; }
        }
    }
}
