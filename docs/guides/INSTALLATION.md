# 📥 Guía de Instalación - ChronoGuard

Esta guía te ayudará a instalar y configurar ChronoGuard en tu sistema Windows.

## 📋 Requisitos del Sistema

### Mínimos
- **Sistema Operativo**: Windows 10 (versión 1903) o superior / Windows 11
- **Procesador**: x64 compatible (Intel/AMD)
- **RAM**: 256 MB disponible
- **Espacio en disco**: 100 MB libres
- **Permisos**: Acceso a configuración de pantalla del sistema

### Recomendados
- **RAM**: 512 MB o más
- **Espacio en disco**: 200 MB libres
- **Conexión a internet**: Para actualizaciones automáticas
- **Múltiples monitores**: Para aprovechar funciones avanzadas

## 🚀 Métodos de Instalación

### Método 1: Instalador MSI (Recomendado)

#### ✅ Ventajas
- ✨ Instalación automática de dependencias
- 🔧 Configuración automática de inicio con Windows
- 🗑️ Desinstalación limpia desde Panel de Control
- 📦 Actualizaciones automáticas

#### 📥 Pasos de Instalación

1. **Descargar el Instalador**
   - Ve a [GitHub Releases](https://github.com/DanielAguilarJ/ChronoGuard/releases/latest)
   - Descarga `ChronoGuard-Setup-v1.0.0.msi`

2. **Ejecutar Instalador**
   - Clic derecho en el archivo MSI
   - Selecciona "Ejecutar como administrador"
   - ⚠️ **Importante**: Se requieren permisos de administrador

3. **Seguir el Asistente**
   ```
   ┌─────────────────────────────────────┐
   │  🌅 ChronoGuard Setup Wizard       │
   ├─────────────────────────────────────┤
   │                                     │
   │  Bienvenido a ChronoGuard           │
   │                                     │
   │  Este asistente te guiará en la     │
   │  instalación de ChronoGuard.        │
   │                                     │
   │  [Siguiente] [Cancelar]             │
   └─────────────────────────────────────┘
   ```

4. **Configuración de Instalación**
   - **Directorio**: `C:\Program Files\ChronoGuard\` (por defecto)
   - **Accesos directos**: ✅ Escritorio, ✅ Menú Inicio
   - **Inicio automático**: ✅ Recomendado

5. **Finalizar Instalación**
   - El instalador configurará todo automáticamente
   - ChronoGuard se iniciará al completarse

### Método 2: Portable (Sin Instalación)

#### ✅ Ventajas
- 🚀 No requiere permisos de administrador
- 💾 Ideal para uso en múltiples equipos
- 🔧 Configuración portátil
- 🚫 No modifica el registro del sistema

#### ⚠️ Requisitos Previos
- .NET 8.0 Runtime debe estar instalado
- Descarga desde: [Microsoft .NET](https://dotnet.microsoft.com/download/dotnet/8.0)

#### 📥 Pasos de Instalación

1. **Descargar la Versión Portable**
   - `ChronoGuard-Portable-v1.0.0-win-x64.zip` (64-bit)
   - `ChronoGuard-Portable-v1.0.0-win-x86.zip` (32-bit)

2. **Extraer Archivos**
   ```bash
   # Crear directorio para ChronoGuard
   mkdir C:\Tools\ChronoGuard
   
   # Extraer archivos ZIP
   # Usar Windows Explorer o herramienta de extracción
   ```

3. **Ejecutar ChronoGuard**
   - Navegar a la carpeta extraída
   - Doble clic en `ChronoGuard.App.exe`

4. **Configuración Inicial**
   - ChronoGuard detectará que es una instalación portable
   - Todas las configuraciones se guardarán en la carpeta local

### Método 3: Compilar desde Código Fuente

#### 🔧 Para Desarrolladores

```bash
# Clonar repositorio
git clone https://github.com/DanielAguilarJ/ChronoGuard.git
cd ChronoGuard

# Restaurar dependencias
dotnet restore ChronoGuard/ChronoGuard.sln

# Compilar en modo Release
dotnet build ChronoGuard/ChronoGuard.sln --configuration Release

# Ejecutar
dotnet run --project ChronoGuard/src/ChronoGuard.App
```

## ⚙️ Configuración Inicial

### 🎯 Primer Inicio

1. **Detección de Ubicación**
   ```
   ┌─────────────────────────────────────┐
   │  🌍 Configuración de Ubicación      │
   ├─────────────────────────────────────┤
   │                                     │
   │  ChronoGuard necesita tu ubicación  │
   │  para calcular horarios solares.    │
   │                                     │
   │  ○ Detección automática (IP)        │
   │  ○ GPS (si está disponible)         │
   │  ○ Manual: Ciudad, País             │
   │                                     │
   │  [Continuar]                        │
   └─────────────────────────────────────┘
   ```

2. **Configuración de Monitores**
   - ChronoGuard detectará automáticamente todos los monitores
   - Puedes configurar cada monitor independientemente
   - Se aplicarán perfiles predeterminados

3. **Configuración de Temperaturas**
   - **Día**: 6500K (luz blanca natural)
   - **Noche**: 3000K (luz cálida)
   - **Transición**: 30 minutos (personalizable)

### 🎨 Configuración Avanzada

#### Horarios Personalizados
```json
{
  "schedule": {
    "sunrise": "06:00",
    "sunset": "19:00",
    "transitionDuration": "00:30:00"
  }
}
```

#### Perfiles de Monitor
```json
{
  "monitors": [
    {
      "id": "DISPLAY1",
      "name": "Monitor Principal",
      "dayTemperature": 6500,
      "nightTemperature": 3000,
      "enabled": true
    }
  ]
}
```

## 🔧 Configuración del Sistema

### Permisos Requeridos

ChronoGuard necesita los siguientes permisos:

- ✅ **Acceso a configuración de pantalla**: Para ajustar temperatura de color
- ✅ **Acceso a ubicación**: Para calcular horarios solares (opcional)
- ✅ **Acceso a red**: Para detección de ubicación por IP (opcional)
- ✅ **Registro de Windows**: Para inicio automático (solo instalador MSI)

### Configuración de Firewall

Si tienes un firewall activo:

1. **Windows Defender Firewall**
   - ChronoGuard se agregará automáticamente durante la instalación
   - Si aparece un diálogo, selecciona "Permitir acceso"

2. **Firewalls de Terceros**
   - Agrega excepción para `ChronoGuard.App.exe`
   - Solo necesita acceso de salida para geolocalización

### Antivirus

Si tu antivirus bloquea ChronoGuard:

1. **Agregar Excepción**
   - Ruta de instalación: `C:\Program Files\ChronoGuard\`
   - Archivo principal: `ChronoGuard.App.exe`

2. **Firma Digital**
   - ChronoGuard está firmado digitalmente
   - Verificar certificado en propiedades del archivo

## 🚨 Solución de Problemas

### Problemas Comunes

#### ❌ Error: "No se puede iniciar la aplicación"

**Causa**: .NET 8.0 Runtime no instalado

**Solución**:
```bash
# Verificar instalación de .NET
dotnet --version

# Si no está instalado, descargar desde:
# https://dotnet.microsoft.com/download/dotnet/8.0
```

#### ❌ Error: "Acceso denegado a configuración de pantalla"

**Causa**: Permisos insuficientes

**Solución**:
1. Clic derecho en ChronoGuard
2. Seleccionar "Ejecutar como administrador"
3. O instalar usando MSI como administrador

#### ❌ Error: "No se detectan monitores"

**Causa**: Drivers de pantalla desactualizados

**Solución**:
1. Actualizar drivers de gráficos
2. Reiniciar el sistema
3. Verificar en Configuración > Sistema > Pantalla

#### ❌ ChronoGuard no inicia con Windows

**Solución para Instalación MSI**:
```bash
# Verificar registro
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v ChronoGuard
```

**Solución para Instalación Portable**:
1. Abrir ChronoGuard
2. Ir a Configuración
3. Habilitar "Iniciar con Windows"

### Logs y Diagnóstico

#### Ubicación de Logs
- **Instalación MSI**: `%APPDATA%\ChronoGuard\Logs\`
- **Portable**: `.\Logs\` (carpeta de instalación)

#### Habilitar Logging Detallado
```json
{
  "logging": {
    "level": "Debug",
    "enableFileLogging": true,
    "maxLogFiles": 10
  }
}
```

### Obtener Ayuda

#### 🐛 Reportar Problemas
1. Ve a [GitHub Issues](https://github.com/DanielAguilarJ/ChronoGuard/issues)
2. Usa la plantilla de bug report
3. Incluye logs y captura de pantalla

#### 💬 Comunidad
- [GitHub Discussions](https://github.com/DanielAguilarJ/ChronoGuard/discussions)
- [Documentación](https://github.com/DanielAguilarJ/ChronoGuard#readme)

## ✅ Verificación de Instalación

### Lista de Verificación

- [ ] ChronoGuard se inicia correctamente
- [ ] Se detectan todos los monitores
- [ ] La ubicación se detecta automáticamente
- [ ] Los ajustes de temperatura funcionan
- [ ] El icono aparece en la bandeja del sistema
- [ ] Las transiciones son suaves
- [ ] La configuración se guarda correctamente

### Prueba Rápida

1. **Iniciar ChronoGuard**
2. **Modo de prueba**: Clic derecho en bandeja > "Modo Noche"
3. **Verificar cambio**: La pantalla debe volverse más cálida
4. **Restaurar**: Clic derecho > "Modo Día"

## 🔄 Actualizaciones

### Automáticas (MSI)
- ChronoGuard verificará automáticamente las actualizaciones
- Notificación cuando haya nueva versión disponible
- Actualización con un clic

### Manuales (Portable)
1. Descargar nueva versión desde GitHub
2. Hacer backup de configuración (`settings.json`)
3. Extraer nueva versión
4. Restaurar configuración

---

**¡Listo! ChronoGuard está instalado y configurado.** 🎉

Para más información, consulta el [README principal](../README.md) o la [documentación completa](https://github.com/DanielAguilarJ/ChronoGuard).
