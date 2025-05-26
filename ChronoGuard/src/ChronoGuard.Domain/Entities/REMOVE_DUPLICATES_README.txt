Elimina los siguientes archivos para resolver duplicados y ambigüedad en la compilación:

- src/ChronoGuard.Domain/Entities/ChronoGuardEntities.cs

Estos tipos ya existen en archivos individuales y deben mantenerse solo en:
- ColorProfile.cs
- Location.cs
- SolarTimes.cs

Luego, elimina también:
- src/ChronoGuard.Domain/Interfaces/IChronoGuardServices.cs

Las interfaces deben mantenerse solo en sus archivos individuales (IColorTemperatureService.cs, ILocationService.cs, IProfileService.cs).
