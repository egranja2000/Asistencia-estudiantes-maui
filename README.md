# Asistencia: QR dinámico con checkpoints (Presencial / Online)

Esqueleto funcional de un sistema de registro de asistencia diario:

| Componente | Tecnología | Usuario |
|---|---|---|
| `database/` | SQL Server 2019+ | — |
| `src/Asistencia.Api` | ASP.NET Core 10: **API REST** (JWT) + **portal web MVC** (cookie) | Docente (web) / App |
| `src/Asistencia.Maui` | .NET MAUI 10 (Android + iOS) | Estudiante |
| `src/Asistencia.Shared` | DTOs y constantes compartidas | — |
| `tests/Asistencia.Api.Tests` | xUnit v3: pruebas de seguridad (hash, tokens del QR) y geocerca | — |

## Estructura de la solución

```
Asistencia.slnx                  Solución (abrir con Visual Studio 2026 o 2022 17.14+)
Directory.Build.props            Propiedades comunes: C# latest, nullable, analizadores
Directory.Packages.props         Versiones de NuGet centralizadas (un solo lugar)
global.json                      Versión del SDK de .NET 10 y runner de pruebas
.editorconfig                    Estilo de código (se aplica al compilar)
database/                        Scripts SQL en orden: 01 esquema, 02 demo, 03 mock, 04 utilidades
docs/GUIA_PRUEBAS.md             Recorrido de pruebas de punta a punta
src/
  Asistencia.Shared/
    Constantes/                  Modalidades, estados, códigos de resultado, formato del QR
    Dtos/                        Contratos request/response entre API y app
  Asistencia.Api/
    Asistencia.Api.http          Peticiones de prueba (se ejecutan desde Visual Studio)
    Configuracion/               Clases de opciones (Jwt, Checkpoints)
    Controllers/Api/             Endpoints REST para la app (JWT)
    Controllers/Web/             Portal del docente (MVC, cookie)
    Extensiones/                 Registro de servicios y middleware (mantiene Program.cs corto)
    Infraestructura/             Conexión SQL, zona horaria, geodistancia
    Modelos/                     Filas que devuelve Dapper
    Seguridad/                   Hash de contraseñas, JWT, tokens rotativos, políticas
    Servicios/                   Lógica de negocio
    ViewModels/                  Modelos de las vistas Razor
    Styles/app.css               Fuente de estilos Tailwind CSS v4 (se compila a wwwroot/css/app.css)
    Ui/Iconos.cs                 Íconos SVG (Heroicons) para las vistas: @Iconos.Svg("calendario")
    Views/, wwwroot/             Vistas y archivos estáticos (JavaScript en wwwroot/js, ver abajo)
  Asistencia.Maui/
    Convertidores/               IValueConverter para XAML
    Servicios/                   Cliente HTTP, sesión, dispositivo, GPS, marcación
    ViewModels/                  MVVM con CommunityToolkit.Mvvm
    Views/                       Páginas XAML
    Platforms/, Resources/       Código y recursos por plataforma
tests/
  Asistencia.Api.Tests/          Pruebas unitarias (carpetas espejo de la API)
```

Convenciones: un tipo por archivo, el namespace coincide con la carpeta, y las versiones de NuGet se cambian solo en `Directory.Packages.props`.

```
 DOCENTE (navegador)                         ESTUDIANTE (app MAUI)
 ─────────────────────                       ─────────────────────
 Crea sesión (Presencial/Online)             Login (vincula su celular)
 Lanza checkpoint ─┐                         ┌─ Escanea QR  ─┐
 Proyecta pantalla │  QR + código 6 dígitos  │  o escribe    │
 (aula o Zoom/Meet)└───── cambian c/20 s ───►│  el código    │
                                             └───────────────┘
                     ┌──────────────────────────────┐   POST /api/estudiante/marcar
 Ve asistencia  ◄────┤  API: valida HMAC + tiempo + │◄──────────────────────────────
 en vivo y ajusta    │  matrícula + dispositivo +   │
                     │  geocerca (solo presencial)  │
                     └──────────────┬───────────────┘
                                    ▼
                               SQL Server
```

---

## 1. Puesta en marcha

### Base de datos
1. Ejecuta `database/01_esquema.sql` en SQL Server (crea `AsistenciaDB`).
2. Ejecuta `database/02_datos_demo.sql`: 1 docente, 3 estudiantes y 1 curso.
   - Docente: `docente@demo.edu` / `Demo123*`
   - Estudiantes: `carlos@demo.edu`, `lucia@demo.edu`, `diego@demo.edu` / `Demo123*`
   - Cambia las coordenadas del aula del curso por las reales.

> **Para probar con datos mock** (historial de 6 semanas, sesiones en curso y simulador de estudiantes), usa `03_datos_mock.sql` + `04_utilidades_prueba.sql` en lugar del 02 y sigue **`docs/GUIA_PRUEBAS.md`**.

### API + portal docente
1. Ajusta `ConnectionStrings:AsistenciaDB` en `src/Asistencia.Api/appsettings.json`.
2. `Jwt:Clave` (mínimo 32 caracteres): en desarrollo ya viene una en `appsettings.Development.json`. En `appsettings.json` está vacía a propósito, así que en producción la API no arranca hasta que la definas en una variable de entorno (`Jwt__Clave`) o en *User Secrets* (clic derecho en el proyecto > **Administrar secretos de usuario**).
3. Ejecuta el proyecto `Asistencia.Api`. Queda escuchando en `http://0.0.0.0:5080` y abre `/Cursos`.

### Estilos del portal (Tailwind CSS)
El CSS ya compilado (`wwwroot/css/app.css`) viene incluido, así que **no necesitas Node para ejecutar el portal**. Solo si cambias clases en las vistas o en `Styles/app.css`, recompílalo (requiere Node.js 20+):

```
cd src/Asistencia.Api
npm install          # una sola vez
npm run css:build    # genera wwwroot/css/app.css minificado
npm run css:watch    # o déjalo corriendo mientras editas vistas
```

JavaScript del portal (sin código JS dentro de las vistas; todo se activa con atributos `data-*`):

```
wwwroot/js/portal.js             Punto de entrada (módulo ES), cargado desde _Layout.cshtml
wwwroot/js/portal/modales.js     Abrir/cerrar/rellenar modales <dialog>
wwwroot/js/portal/confirmar.js   Confirmación con modal antes de enviar un formulario
wwwroot/js/portal/avisos.js      Avisos (TempData) que se ocultan solos
wwwroot/js/portal/grid.js        Grids: búsqueda, orden, paginación, exportar CSV
wwwroot/js/portal/formularios.js Formulario de sesión, botones de valor rápido, filas clicables
wwwroot/js/proyectar.js          Pantalla de proyección del QR
```

Componentes reutilizables (definidos en `Styles/app.css` y `wwwroot/js/portal/`):
- **Botones y campos:** `btn btn-primario`, `btn-secundario`, `btn-peligro`, `campo`, `etiqueta`, `tarjeta`, `insignia estado-presente`…
- **Modales** (`<dialog class="modal">`): un botón con `data-abrir-modal="idModal"` lo abre; `data-cerrar-modal` lo cierra; `data-rellenar='{"campo":"valor"}'` precarga sus campos. Un `<form data-confirmar="¿Seguro?">` pide confirmación con el modal común.
- **Grids** (`<div class="grid-datos" data-grid>`): búsqueda sin tildes, orden por columna (`th data-ordenable`), paginación y exportar a CSV (`data-grid-exportar`).

### App MAUI
La paleta (indigo/slate, igual que el portal) y los estilos están centralizados en `App.xaml`.

1. En `Constantes.cs` revisa `ApiUrl`:
   - Emulador Android: `http://10.0.2.2:5080/` (ya configurado).
   - Celular físico: `http://<IP-de-tu-PC>:5080/`. Agrega esa IP también en `Platforms/Android/Resources/xml/network_security_config.xml` y abre el puerto 5080 en el firewall de Windows.
2. Cambia `ApplicationId` en el `.csproj`.
3. Ejecuta en Android o iOS.

### Pruebas
- Visual Studio: **Prueba > Explorador de pruebas > Ejecutar todo**.
- Consola: `dotnet test --project tests/Asistencia.Api.Tests/Asistencia.Api.Tests.csproj`.

> **Estado de compilación:** la API, Shared y las pruebas compilan sin errores con el SDK de .NET 10 (18 pruebas en verde). El código C# y XAML de la app MAUI también compila; el paquete final para Android o iOS se genera desde Visual Studio con el workload de MAUI instalado.

---

## 2. Cómo funciona el token del checkpoint

Cada checkpoint tiene un **secreto aleatorio de 32 bytes** que nunca sale del servidor. Funciona igual que TOTP (Google Authenticator):

```
paso   = segundosUnix / RotacionSegundos           (cambia cada 20 s)
QR     = ASIS1.{checkpointId}.{paso}.{HMAC-SHA256(secreto, "qr|id|paso") → 16 bytes base64url}
Código = 6 dígitos = truncamiento dinámico de HMAC-SHA256(secreto, "code|id|paso")  (RFC 4226)
```

- La API acepta el paso actual y **1 paso anterior** (`PasosTolerancia`), lo que da una validez real de 20 a 40 s.
- Una foto del QR enviada por WhatsApp caduca antes de que sirva.
- Para el código de 6 dígitos, la API prueba solo contra los checkpoints **abiertos en cursos donde el estudiante está matriculado**. Además hay *rate limiting* de 10 intentos por minuto por estudiante, así que la fuerza bruta no es viable.

## 3. Validaciones al marcar (`AsistenciaService.MarcarAsync`)

| # | Validación | Código si falla |
|---|---|---|
| 1 | El dispositivo del JWT es el vinculado al estudiante | `DISPOSITIVO_NO_VINCULADO` |
| 2 | Firma HMAC válida y paso vigente | `QR_INVALIDO`, `CODIGO_INVALIDO`, `TOKEN_EXPIRADO` |
| 3 | Checkpoint abierto (+10 s de gracia) y sesión no cerrada | `CHECKPOINT_CERRADO` |
| 4 | Matrícula activa en el curso | `NO_MATRICULADO` |
| 5 | Registro previo en ese checkpoint | `YA_REGISTRADO` (cuenta como éxito) |
| 6 | **Solo presencial con geocerca:** GPS dentro del radio, sin *mock location* y con precisión ≤ 100 m | `UBICACION_REQUERIDA`, `UBICACION_SIMULADA`, `FUERA_DE_RANGO` |
| 7 | `UNIQUE (CheckpointId, EstudianteId)` y `UNIQUE (CheckpointId, DeviceId)` | — |

Cada rechazo se guarda en `IntentoRechazado`, y el docente ve el resumen en el detalle de la sesión.

**Ubicación solo cuando hace falta:** la app envía la marcación sin GPS. Si la sesión es presencial con geocerca, la API responde `UBICACION_REQUERIDA` y entonces la app pide el permiso y reenvía con la ubicación. En clases online **nunca** se solicita.

## 4. Diferencias por modalidad

| | Presencial | Online |
|---|---|---|
| Dónde se muestra el QR | Proyector del aula | Pantalla compartida en Zoom/Teams/Meet |
| Método del estudiante | Escanear QR | QR desde otro equipo **o código de 6 dígitos** |
| Checkpoints recomendados | 1 al inicio, de 5 min | 2-3 **automáticos aleatorios** de 60 s |
| Geocerca GPS | Opcional (`ValidarUbicacion`) | No aplica |
| Riesgo principal | Foto del QR a un amigo | Conectarse, marcar y desaparecer |

**Estado final por sesión** (vista `vw_AsistenciaSesion`):
- Validó todos los checkpoints: **Presente**
- Validó al menos uno: **Parcial**
- No validó ninguno: **Ausente**

El docente puede **ajustar manualmente** a cualquier estado (por ejemplo, *Justificado* o una falla de red), y ese ajuste tiene prioridad sobre el cálculo.

`vw_PorcentajeAsistencia` calcula el % por estudiante sobre sesiones cerradas. Presente y Justificado valen 1, Parcial 0,5 y Ausente 0. El portal resalta a quienes están por debajo del 80 %.

## 5. Flujo del docente (web)

1. **Mis cursos → curso → Nueva sesión**: modalidad, horario, enlace de la reunión (online) o geocerca (presencial), y checkpoints automáticos.
2. **Detalle de sesión → Lanzar checkpoint ahora**: abre la pantalla de **proyección** con QR grande, código de 6 dígitos, cuenta regresiva y contador de registrados.
3. Online: comparte esa pestaña en la videollamada **durante toda la clase**, porque los checkpoints automáticos aparecen en ella sin aviso.
4. **Cerrar sesión**: corta el checkpoint activo, cancela los que no empezaron y consolida la asistencia.
5. Si un estudiante cambia de celular, usa **desvincular celular** junto a su nombre.

## 6. Flujo del estudiante (app)

1. **Login** con correo o cédula. El primer ingreso vincula el celular a la cuenta.
2. **Inicio**: muestra checkpoints abiertos ahora en sus cursos (se refresca cada 15 s) e historial de 30 días.
3. **Escanear QR** o **Ingresar código** → pantalla grande de ✓ o ✕ con el motivo.

## 7. Endpoints

| Método | Ruta | Auth | Uso |
|---|---|---|---|
| POST | `/api/auth/login` | — | Login del estudiante y vínculo del dispositivo |
| GET | `/api/estudiante/checkpoints-activos` | JWT | Checkpoints abiertos ahora |
| POST | `/api/estudiante/marcar` | JWT | Marcar (`Metodo`: `QR` o `Codigo`) |
| GET | `/api/estudiante/historial?dias=30` | JWT | Historial |
| GET | `/Sesiones/Estado/{id}` | Cookie | Estado de la proyección (polling cada 2 s) |

## 8. Configuración (`appsettings.json` → `Checkpoints`)

| Clave | Default | Significado |
|---|---|---|
| `RotacionSegundos` | 20 | Cada cuánto cambian el QR y el código |
| `PasosTolerancia` | 1 | Pasos anteriores aceptados |
| `GraciaSegundos` | 10 | Margen tras el cierre del checkpoint |
| `DuracionOnlineSegundos` | 60 | Duración sugerida online |
| `DuracionPresencialSegundos` | 300 | Duración sugerida presencial |
| `PrecisionMaximaMetros` | 100 | Peor precisión GPS aceptada |

## 9. Pendiente o siguientes pasos

- **CRUD de cursos, estudiantes y matrículas.** Hoy se cargan por SQL. Puede ser un módulo admin o una importación desde Excel.
- **Exportar a Excel** el porcentaje por curso. Las grillas encajan bien con un `DataGrid` de DevExtreme.
- **Refresh tokens.** Hoy el JWT dura 30 días.
- **Notificación push** al abrirse un checkpoint automático (Firebase/APNs). Hoy la app consulta cada 15 s mientras está abierta.
- **Sin modo offline a propósito:** un token que cambia cada 20 s solo se puede validar en línea. Si falla la red, el respaldo es el **ajuste manual** del docente.
- **Cruce con reportes de Zoom/Teams** (minutos conectados) para detectar a quien marca y se va.
- En producción: HTTPS obligatorio, quitar las excepciones *cleartext* de Android e iOS y guardar la clave JWT fuera del repositorio.
