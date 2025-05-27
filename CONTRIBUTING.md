# 🤝 Guía de Contribución - ChronoGuard

¡Gracias por tu interés en contribuir a ChronoGuard! Valoramos todas las contribuciones, desde corrección de bugs hasta nuevas características.

## 📋 Tabla de Contenidos

- [🚀 Primeros Pasos](#-primeros-pasos)
- [🏗️ Configuración del Entorno](#️-configuración-del-entorno)
- [🔄 Proceso de Contribución](#-proceso-de-contribución)
- [📝 Estándares de Código](#-estándares-de-código)
- [🧪 Testing](#-testing)
- [📖 Documentación](#-documentación)
- [🐛 Reportar Issues](#-reportar-issues)
- [💡 Solicitar Features](#-solicitar-features)

## 🚀 Primeros Pasos

### Prerequisitos

- **Visual Studio 2022** (Community o superior) con workload de .NET Desktop Development
- **.NET 8.0 SDK** o superior
- **Git** para control de versiones
- **Windows 10/11** para testing (WPF requiere Windows)

### Fork y Clone

1. **Fork** el repositorio en GitHub
2. **Clone** tu fork localmente:
   ```bash
   git clone https://github.com/tu-usuario/ChronoGuard.git
   cd ChronoGuard
   ```
3. **Configura** el upstream:
   ```bash
   git remote add upstream https://github.com/DanielAguilarJ/ChronoGuard.git
   ```

## 🏗️ Configuración del Entorno

### 1. Restaurar Dependencias

```bash
dotnet restore
```

### 2. Verificar Compilación

```bash
dotnet build --configuration Debug
```

### 3. Ejecutar Tests

```bash
dotnet test
```

### 4. Ejecutar la Aplicación

```bash
dotnet run --project src/ChronoGuard.App
```

## 🔄 Proceso de Contribución

### 1. Crear una Rama

```bash
git checkout -b feature/nombre-descriptivo
# o
git checkout -b fix/descripcion-del-bug
```

### 2. Convenciones de Ramas

- `feature/` - Nuevas características
- `fix/` - Corrección de bugs
- `docs/` - Cambios en documentación
- `refactor/` - Refactoring de código
- `test/` - Añadir o mejorar tests

### 3. Hacer Cambios

- Escribe código siguiendo nuestros [estándares](#-estándares-de-código)
- Añade tests para nueva funcionalidad
- Actualiza documentación si es necesario
- Haz commits frecuentes y descriptivos

### 4. Commits Convencionales

Usamos [Conventional Commits](https://www.conventionalcommits.org/):

```bash
feat: añadir soporte para perfiles de monitor personalizados
fix: corregir crash al detectar monitores HDR
docs: actualizar guía de instalación
style: formatear código en MonitorService
refactor: extraer lógica de cálculo solar a servicio separado
test: añadir tests para ColorTemperatureService
```

### 5. Push y Pull Request

```bash
git push origin feature/nombre-descriptivo
```

Luego crea un Pull Request desde GitHub con:
- **Título descriptivo** siguiendo convenciones
- **Descripción detallada** de cambios
- **Screenshots** si hay cambios en UI
- **Referencia a issues** relacionados

## 📝 Estándares de Código

### C# Style Guidelines

- **Naming**: PascalCase para públicos, camelCase para privados
- **Indentación**: 4 espacios, no tabs
- **Líneas**: Máximo 120 caracteres
- **Documentación**: XML docs para APIs públicas

```csharp
/// <summary>
/// Calcula la temperatura de color basada en la hora del día
/// </summary>
/// <param name="currentTime">Hora actual</param>
/// <param name="location">Ubicación geográfica</param>
/// <returns>Temperatura en Kelvin</returns>
public int CalculateColorTemperature(DateTime currentTime, GeographicLocation location)
{
    // Implementación...
}
```

### XAML Guidelines

- **Indentación**: 2 espacios
- **Naming**: PascalCase para elementos con nombre
- **Recursos**: Usa StaticResource en lugar de DynamicResource cuando sea posible

```xaml
<Grid Background="{StaticResource PrimaryBackgroundBrush}">
  <TextBlock Text="{Binding DisplayName}"
             Style="{StaticResource HeadingTextStyle}" />
</Grid>
```

### Arquitectura

- **MVVM**: Mantén separación entre View, ViewModel y Model
- **DI**: Usa inyección de dependencias para servicios
- **Async/Await**: Para operaciones I/O y de larga duración
- **Exception Handling**: Maneja excepciones apropiadamente

## 🧪 Testing

### Estructura de Tests

```
ChronoGuard.Tests/
├── Unit/
│   ├── Services/
│   ├── ViewModels/
│   └── Utilities/
├── Integration/
├── Fixtures/
└── TestData/
```

### Writing Tests

```csharp
[Test]
public async Task CalculateColorTemperature_DuringDay_ReturnsHighTemperature()
{
    // Arrange
    var service = new ColorTemperatureService();
    var noonTime = DateTime.Today.AddHours(12);
    var location = new GeographicLocation(40.7128, -74.0060);

    // Act
    var temperature = await service.CalculateColorTemperature(noonTime, location);

    // Assert
    Assert.That(temperature, Is.GreaterThan(5000));
}
```

### Test Categories

- **Unit Tests**: Lógica de negocio aislada
- **Integration Tests**: Interacción entre componentes
- **UI Tests**: Flujos de usuario (si aplica)

## 📖 Documentación

### Code Documentation

- **XML Comments** para APIs públicas
- **README** para módulos complejos
- **Inline comments** para lógica compleja

### User Documentation

- Actualiza **README.md** para nuevas características
- Añade **screenshots** para cambios de UI
- Documenta **breaking changes** en CHANGELOG.md

## 🐛 Reportar Issues

### Template para Bugs

```markdown
**Descripción**
Descripción clara y concisa del bug.

**Pasos para Reproducir**
1. Ir a '...'
2. Hacer clic en '....'
3. Scroll down to '....'
4. Ver error

**Comportamiento Esperado**
Descripción de lo que esperabas que pasara.

**Screenshots**
Si aplica, añade screenshots para explicar el problema.

**Información del Sistema:**
 - OS: [e.g. Windows 11]
 - Versión ChronoGuard: [e.g. 1.0.0]
 - .NET Version: [e.g. 8.0]
 - Hardware: [e.g. NVIDIA RTX 4070, Dual monitor setup]

**Información Adicional**
Cualquier otro contexto sobre el problema.
```

### Labels para Issues

- `bug` - Algo no funciona
- `enhancement` - Nueva característica o request
- `documentation` - Mejoras en documentación
- `good first issue` - Bueno para nuevos contribuidores
- `help wanted` - Ayuda extra deseada
- `priority:high` - Alta prioridad
- `priority:low` - Baja prioridad

## 💡 Solicitar Features

### Template para Features

```markdown
**¿Tu solicitud está relacionada a un problema?**
Descripción clara de cuál es el problema. Ej. Siempre me frustra cuando [...]

**Describe la solución que te gustaría**
Descripción clara y concisa de lo que quieres que pase.

**Describe alternativas consideradas**
Descripción de soluciones o características alternativas que has considerado.

**Información Adicional**
Cualquier otro contexto o screenshots sobre la solicitud.

**Casos de Uso**
- Como [tipo de usuario], quiero [alguna meta] para que [alguna razón]
- Como [tipo de usuario], quiero [alguna meta] para que [alguna razón]

**Criterios de Aceptación**
- [ ] Criterio 1
- [ ] Criterio 2
- [ ] Criterio 3
```

## 🎯 Prioridades Actuales

### High Priority
- 🔧 Soporte para monitores HDR
- 🌍 API de geolocalización mejorada
- 📱 Companion app para móvil
- 🎨 Temas personalizables

### Medium Priority
- 📊 Analytics y telemetría
- 🔌 Plugin system
- 🌐 Sincronización en la nube
- 📝 Configuración exportable

### Low Priority
- 🎮 Gaming mode optimizations
- 📺 TV display support
- 🔊 Audio-based adjustments
- 🤖 Machine learning profiles

## 🏷️ Versionado

Seguimos [Semantic Versioning](https://semver.org/):

- **MAJOR**: Cambios incompatibles en API
- **MINOR**: Funcionalidad nueva compatible hacia atrás
- **PATCH**: Bug fixes compatibles hacia atrás

## 🎉 Reconocimientos

### Contribuidores

Los contribuidores son reconocidos en:
- 📄 **README.md** - Contributors section
- 📋 **CHANGELOG.md** - Release notes
- 🏆 **GitHub Profile** - Contributions graph

### First-time Contributors

¡Especialmente damos la bienvenida a nuevos contribuidores! Busca issues etiquetados como `good first issue`.

## 📞 Contacto

¿Tienes preguntas? ¡Contáctanos!

- 💬 **GitHub Discussions** - Para preguntas generales
- 📧 **Email** - [contributing@chronoguard.app](mailto:contributing@chronoguard.app)
- 🐦 **Twitter** - [@ChronoGuardApp](https://twitter.com/ChronoGuardApp)

---

**¡Gracias por contribuir a ChronoGuard! 🙏**
