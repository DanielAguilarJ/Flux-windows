# 🌅 ChronoGuard - Monitor Color Temperature Management

<div align="center">

![ChronoGuard Logo](ChronoGuard/src/ChronoGuard.App/Assets/chronoguard.ico)

**Protege tus ojos con ajustes automáticos de temperatura de color basados en la hora del día**

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0-blue.svg)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Windows](https://img.shields.io/badge/Platform-Windows-blue.svg)](https://www.microsoft.com/windows)
[![Release](https://img.shields.io/github/v/release/DanielAguilarJ/ChronoGuard)](https://github.com/DanielAguilarJ/ChronoGuard/releases)

[📥 Descargar](#-descarga) • [📖 Características](#-características) • [🚀 Instalación](#-instalación) • [📸 Capturas](#-capturas-de-pantalla) • [🤝 Contribuir](#-contribuir)

</div>

---

## 🌟 Descripción

**ChronoGuard** es una aplicación avanzada de gestión de temperatura de color para Windows que ajusta automáticamente la temperatura de color de tus monitores basándose en la hora del día, tu ubicación geográfica y las condiciones de iluminación. Diseñada con una interfaz moderna utilizando Fluent Design System y glassmorfismo.

### ✨ ¿Por qué ChronoGuard?

- 🔵 **Reduce la fatiga ocular** durante el trabajo nocturno
- 🌙 **Mejora la calidad del sueño** filtrando luz azul por la noche
- 🌍 **Ajustes automáticos** basados en tu ubicación y zona horaria
- 🎨 **Interfaz moderna** con diseño Fluent y efectos de transparencia
- ⚡ **Alto rendimiento** con mínimo impacto en recursos del sistema

## 🎯 Características

### 🌅 Gestión Automática de Color
- **Ajuste solar automático**: Basado en hora de amanecer y atardecer
- **Ubicación inteligente**: Detección automática por IP o GPS
- **Perfiles personalizables**: Crea configuraciones para diferentes actividades
- **Transiciones suaves**: Cambios graduales para evitar molestias

### 🖥️ Soporte Multi-Monitor
- **Detección automática** de todos los monitores conectados
- **Configuración independiente** para cada pantalla
- **Perfiles ICC** y gestión avanzada de color
- **Calibración profesional** con soporte para colorímetros

### 🎨 Interfaz de Nueva Generación
- **Fluent Design System** con efectos de profundidad
- **Glassmorfismo** profesional con transparencias
- **Modo oscuro** con paleta Catppuccin Mocha
- **Dashboard responsivo** con métricas en tiempo real
- **Animaciones fluidas** y transiciones suaves

### ⚙️ Características Avanzadas
- **Bandeja del sistema** con controles rápidos
- **Inicio automático** con Windows
- **API REST** para integración con otros sistemas
- **Monitoreo de rendimiento** y métricas del sistema
- **Configuración en la nube** (próximamente)

## 📥 Descarga

### 🚀 Versión Estable (Recomendada)

<div align="center">

[![Descargar ChronoGuard](https://img.shields.io/badge/Descargar-ChronoGuard%20v1.0.0-success?style=for-the-badge&logo=download)](https://github.com/DanielAguilarJ/ChronoGuard/releases/latest)

</div>

### 📋 Requisitos del Sistema

- **OS**: Windows 10 (1903) o superior / Windows 11
- **Framework**: .NET 8.0 Runtime (incluido en el instalador)
- **RAM**: 256 MB mínimo, 512 MB recomendado
- **Espacio**: 100 MB de espacio libre
- **Permisos**: Acceso a configuración de pantalla del sistema

### 📦 Opciones de Instalación

1. **Instalador MSI** (Recomendado)
   - Instalación automática de dependencias
   - Configuración de inicio automático
   - Desinstalación limpia desde Panel de Control

2. **Portable ZIP**
   - No requiere instalación
   - Ideal para uso en múltiples equipos
   - Requiere .NET 8.0 Runtime instalado

3. **Microsoft Store** (Próximamente)
   - Actualizaciones automáticas
   - Instalación sandboxed
   - Soporte para Windows 10S

## 🚀 Instalación

### Instalación Rápida (MSI)

1. Descarga el archivo `ChronoGuard-Setup-v1.0.0.msi`
2. Ejecuta el instalador como administrador
3. Sigue el asistente de instalación
4. ChronoGuard se iniciará automáticamente

### Instalación Portable

1. Descarga `ChronoGuard-Portable-v1.0.0.zip`
2. Extrae en la carpeta deseada
3. Ejecuta `ChronoGuard.App.exe`
4. Configura inicio automático desde las opciones

### Compilación desde Código Fuente

```bash
# Clonar el repositorio
git clone https://github.com/DanielAguilarJ/ChronoGuard.git
cd ChronoGuard

# Restaurar dependencias
dotnet restore

# Compilar
dotnet build --configuration Release

# Ejecutar
dotnet run --project src/ChronoGuard.App
```

## 📸 Capturas de Pantalla

<div align="center">

### 🎨 Interfaz Principal
![Interfaz Principal](docs/screenshots/main-interface.png)
*Dashboard principal con métricas en tiempo real y efectos de glassmorfismo*

### 🖥️ Gestión de Monitores
![Gestión de Monitores](docs/screenshots/monitor-management.png)
*Configuración independiente para cada monitor con perfiles ICC*

### ⚙️ Configuración Avanzada
![Configuración](docs/screenshots/settings.png)
*Panel de configuración con opciones avanzadas y personalización*

### 📊 Monitoreo de Rendimiento
![Performance](docs/screenshots/performance.png)
*Métricas del sistema y monitoreo en tiempo real*

</div>

## 🎮 Uso Rápido

### 🌅 Configuración Inicial

1. **Primera ejecución**: ChronoGuard detectará automáticamente tu ubicación
2. **Configurar horarios**: Los horarios solares se calcularán automáticamente
3. **Ajustar temperaturas**: Personaliza las temperaturas para día (6500K) y noche (3000K)
4. **Probar transiciones**: Usa el botón de vista previa para probar los cambios

### ⚡ Controles Rápidos

- **Pausar/Reanudar**: `Ctrl + Shift + P`
- **Modo noche inmediato**: `Ctrl + Shift + N`
- **Modo día inmediato**: `Ctrl + Shift + D`
- **Abrir configuración**: `Ctrl + Shift + S`

### 🔧 Personalización Avanzada

```json
{
  "DayTemperature": 6500,
  "NightTemperature": 3000,
  "TransitionDuration": 1800,
  "AutoStart": true,
  "MinimizeToTray": true,
  "SmoothTransitions": true,
  "LocationSource": "Automatic"
}
```

## 🏗️ Arquitectura

ChronoGuard está construido con arquitectura moderna y patrones de diseño sólidos:

### 🧱 Tecnologías Utilizadas

- **Frontend**: WPF con Fluent Design System
- **Backend**: .NET 8.0 con Clean Architecture
- **Base de datos**: SQLite para configuración local
- **APIs**: Windows Color Management, WMI, Geolocation
- **UI/UX**: Glassmorfismo, Catppuccin color palette

### 📐 Patrones de Diseño

- **MVVM** (Model-View-ViewModel)
- **Dependency Injection** con Microsoft.Extensions.DependencyInjection
- **Command Pattern** para acciones de UI
- **Observer Pattern** para eventos del sistema
- **Strategy Pattern** para diferentes fuentes de ubicación

## 🤝 Contribuir

¡Las contribuciones son bienvenidas! Por favor lee nuestra [Guía de Contribución](CONTRIBUTING.md) antes de enviar PRs.

### 🐛 Reportar Bugs

1. Verifica que el bug no esté ya reportado
2. Usa la plantilla de issue para bugs
3. Incluye logs y capturas de pantalla
4. Especifica versión de Windows y hardware

### 💡 Solicitar Características

1. Busca si la característica ya fue solicitada
2. Usa la plantilla de issue para características
3. Describe el caso de uso y beneficios
4. Proporciona mockups si es posible

### 🔧 Desarrollo

```bash
# Fork del repositorio
# Clonar tu fork
git clone https://github.com/tu-usuario/ChronoGuard.git

# Crear rama para característica
git checkout -b feature/nueva-caracteristica

# Hacer cambios y commit
git commit -m "feat: nueva característica increíble"

# Push y crear PR
git push origin feature/nueva-caracteristica
```

## 📄 Licencia

Este proyecto está licenciado bajo la Licencia MIT - ver el archivo [LICENSE](LICENSE) para detalles.

## 🙏 Reconocimientos

- **Microsoft** - Por el excelente framework WPF y .NET
- **Catppuccin** - Por la hermosa paleta de colores
- **Fluent Design System** - Por las guías de diseño moderno
- **Comunidad Open Source** - Por las librerías y herramientas utilizadas

## 📞 Soporte

### 💬 Comunidad

- **GitHub Discussions**: Para preguntas generales y discusiones
- **Issues**: Para bugs y solicitudes de características
- **Discord**: [Únete a nuestro servidor](https://discord.gg/chronoguard) (próximamente)

### 📧 Contacto

- **Email**: support@chronoguard.app
- **Twitter**: [@ChronoGuardApp](https://twitter.com/ChronoGuardApp)
- **Website**: [www.chronoguard.app](https://www.chronoguard.app) (próximamente)

---

<div align="center">

**Hecho con ❤️ para proteger tus ojos**

⭐ **¡Si ChronoGuard te resulta útil, considera darle una estrella!** ⭐

</div>