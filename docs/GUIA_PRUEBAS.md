# Guía de pruebas con datos mock

Recorrido de pruebas de punta a punta: **BD → portal docente → API → app**.
Duración aproximada: 45 minutos.

## 0. Preparación (una sola vez)

| Paso | Acción |
|---|---|
| 1 | Ejecuta `database/01_esquema.sql` (si la BD no existe). |
| 2 | Ejecuta `database/03_datos_mock.sql`. **Borra y regenera todo.** Anota los `SesionId` S1-S5 que imprime. |
| 3 | Ejecuta `database/04_utilidades_prueba.sql` (crea el esquema `demo` y muestra las consultas de verificación). |
| 4 | Corre `Asistencia.Api` en **Development** (perfil `http`, puerto 5080). |
| 5 | Corre la app MAUI en el emulador Android o en tu celular (revisa `Constantes.ApiUrl`). |

> Las sesiones de hoy se crean **relativas a la hora en que ejecutas el script 03**. Si pasan más de 2 horas, el checkpoint activo de S1 vence. En ese caso vuelve a ejecutar 03, o abre uno nuevo con `EXEC demo.sp_AbrirCheckpoint @SesionId = <S1>, @Minutos = 60;`.

**Usuarios** (todos con la contraseña `Demo123*`)

| Usuario | Rol | Uso en las pruebas |
|---|---|---|
| `docente@demo.edu` | Docente | Portal web. Cursos PROG-101, BD-201 y WEB-301 |
| `docente2@demo.edu` | Docente | Dueño de MAT-110. Sirve para probar el aislamiento |
| `carlos@demo.edu` | Estudiante, perfil alto | Archivo `.http` |
| `lucia@demo.edu` | Estudiante, perfil medio | **Celular / emulador** |
| `diego@demo.edu` | Estudiante, perfil bajo, **no** matriculado en WEB-301 | Celular (casos de error) |
| `est01…est22@demo.edu` | Estudiantes mock con dispositivo ficticio | Simulador web |

**Sesiones de hoy**

| | Curso | Situación | Para probar |
|---|---|---|---|
| S1 | PROG-101 | Presencial en curso, checkpoint #2 **activo 2 h**, sin geocerca | Escaneo QR y código desde cualquier lugar |
| S2 | BD-201 | Online en curso; checkpoints automáticos en **+12 min** y **+70 min** | Aparición automática y estado Parcial |
| S3 | WEB-301 | Presencial **con geocerca**, programada | GPS, FUERA_DE_RANGO, NO_MATRICULADO |
| S4 | PROG-101 | Mañana | Sesión programada |
| S5 | MAT-110 | De **otro docente** | Aislamiento (no debe verse) |

---

## 1. Base de datos

| # | Prueba | Resultado esperado |
|---|---|---|
| BD1 | Resumen al final del script 03 | 27 usuarios, 4 cursos, 60 matrículas, ~47 sesiones, ~78 checkpoints, ~1.000 marcaciones, ~40 rechazos y algunas decenas de ajustes |
| BD2 | Consulta 3 de `04` (distribución) | En presencial predomina Presente. En online aparecen bastantes **Parcial** |
| BD3 | Consulta 2 de `04` (porcentajes) | Esperado aprox.: alto ~94 %, Lucía ~88 %, Diego ~73 % en presencial / ~59 % online, perfil bajo (est07/14/21) ~70 % / ~57 %. En BD-201 (online) también los perfiles medios (est05/15/20) caen a ~67 % por los "conectarse y desaparecer" |
| BD4 | Consulta 4 de `04` (integridad) | **0 filas** |
| BD5 | `EXEC demo.sp_Estado;` | S1 y S5 con checkpoint abierto; S2 con próximos automáticos |

## 2. Portal docente (http://localhost:5080)

| # | Prueba | Resultado esperado |
|---|---|---|
| W1 | Login con `docente@demo.edu` y contraseña mala | "Usuario o contraseña incorrectos." |
| W2 | Login correcto | 3 cursos. **MAT-110 no aparece** |
| W3 | Abrir PROG-101 | Lista de sesiones (cerradas, S1 abierta, S4 programada) y tabla de % con filas amarillas < 80 % |
| W4 | Abrir **S1** | Checkpoint #1 cerrado con marcas, checkpoint #2 **Activo**. Los mock que marcaron solo el #1 salen como **Parcial** |
| W5 | **Abrir proyección** de S1 | QR grande y código de 6 dígitos. Ambos cambian cada 20 s y la barra baja. Contador "x / 18" |
| W6 | Simulador: 75 % + "Incluir intentos inválidos" | Mensaje `REGISTRADO × n · TOKEN_EXPIRADO × 1 · QR_INVALIDO × 1 · CODIGO_INVALIDO × 1 · DISPOSITIVO_NO_VINCULADO × 1`. El contador de la proyección sube en ≤ 2 s |
| W7 | Volver a S1 | La tabla "Intentos rechazados" muestra los códigos de W6 |
| W8 | Simular de nuevo al 100 % | Marca a los mock que faltaban. Una tercera vez dice "No quedan estudiantes mock pendientes" |
| W9 | Ajuste manual: un Ausente → Justificado con motivo | Estado "Justificado" y debajo "manual (calc.: Ausente)". Elegir "Automático" lo revierte |
| W10 | Abrir **S2** y su proyección | "Esperando el próximo checkpoint…". A los ~12 min el QR aparece **solo**, dura 60 s y vuelve a "Esperando" |
| W11 | En S2, Simular durante el checkpoint automático | La mitad marca con código y la otra con QR (online) |
| W12 | URL `/Sesiones/Detalle/<S5>` | **404**, porque es de otro docente |
| W13 | Crear sesión online con 3 automáticos | En el detalle aparecen 3 checkpoints "Programado" en horas aleatorias, repartidos en la clase |
| W14 | Crear sesión presencial con hora fin < inicio | Error "La hora de fin debe ser posterior…" |
| W15 | Lanzar checkpoint cuando ya hay uno activo | Botón deshabilitado: "Hay un checkpoint activo" |
| W16 | **Cerrar** S1 | Estado Cerrada, la proyección dice "La sesión está cerrada" y PROG-101 recalcula los % (incluye S1) |

## 3. API (archivo `src/Asistencia.Api/Asistencia.Api.http`)

Ejecuta las peticiones **en orden**. A1 vincula a Carlos con el dispositivo del archivo.

| # | Petición | Resultado esperado |
|---|---|---|
| A1–A2 | Login con correo y con cédula | 200 + token |
| A3 | Contraseña mala | 400 |
| A4 | Carlos desde otro dispositivo | 400 "vinculada a otro dispositivo" |
| A5 | Diego con el dispositivo de Carlos | 400 "ya está vinculado a la cuenta de otro estudiante" |
| A6 | Docente en la app | 400 "solo para estudiantes" |
| A7 | Sin token | 401 |
| B1 | Checkpoints activos | PROG-101 (S1). BD-201 solo durante sus ventanas automáticas |
| B2 | Historial | Mezcla de estados de las últimas semanas |
| C1 | Código copiado de la proyección de S1 | `REGISTRADO` |
| C2 | Repetir | `YA_REGISTRADO`, `exito: true` |
| C4–C7 | Formatos inválidos | `CODIGO_INVALIDO` / `QR_INVALIDO` |
| C8 | Valor vacío | 400 |
| C9 | C4 más de 10 veces en 1 minuto | **429** |

Si C1 devuelve `CODIGO_INVALIDO`, el código cambió antes de enviarlo. Cópialo apenas cambie (la barra llena) y vuelve a intentar.

## 4. App MAUI (Lucía y Diego)

Lucía va en el celular y Diego en el emulador (o en un segundo celular). Si solo tienes un dispositivo, haz primero los casos de Lucía. Después cierra sesión, ejecuta `EXEC demo.sp_DesvincularDemo;` y entra como Diego.

| # | Prueba | Resultado esperado |
|---|---|---|
| M1 | Login `lucia@demo.edu` | Entra a "Mi asistencia". Queda vinculada a ese dispositivo |
| M2 | Inicio | "Abiertos ahora: PROG-101 · Presencial · checkpoint #2 · Pendiente" e historial de 30 días con colores |
| M3 | **Escanear QR** apuntando a la proyección de S1 (monitor de la PC) | Pantalla verde "¡Listo!" con curso y hora. En la web el contador sube |
| M4 | Volver a escanear | Verde "Tu asistencia ya estaba registrada." |
| M5 | Inicio | El checkpoint aparece "✓ Registrado" |
| M6 | Toma una **foto** del QR con otro celular, espera 1 minuto y escanéala con la app (Lucía) | Roja: "El QR expiró…" (`TOKEN_EXPIRADO`). El token se valida antes que el registro previo |
| M7 | Diego: **Ingresar código** con un número inventado | Roja "Código incorrecto o expirado" |
| M8 | Diego: código correcto de S1 | Verde |
| M9 | S2: espera el automático (~12 min). La app refresca cada 15 s | Aparece "BD-201 · Online · checkpoint #2". Ingresa el código de la proyección: verde. No pide ubicación (online) |
| M10 | Web, S3 (geocerca WEB-301): **Lanzar checkpoint**. Lucía escanea | La app **pide permiso de ubicación**. Roja "Estás a N m del aula" (`FUERA_DE_RANGO`) |
| M11 | `EXEC demo.sp_ConfigurarGeocerca @SesionId=<S3>, @Lat=<tu lat>, @Lng=<tu lng>, @Radio=60;` y escanea de nuevo | Verde. En la BD la marcación guarda lat/long y distancia |
| M12 | Diego escanea el QR de S3 | Roja "No estás matriculado en Desarrollo Web…" (`NO_MATRICULADO`) |
| M13 | Celular físico con app de *fake GPS* activa, en S3 | Roja "Se detectó una ubicación simulada" (`UBICACION_SIMULADA`) |
| M14 | Login de Lucía en **otro** emulador o celular | Error "vinculada a otro dispositivo". Se resuelve desde la web con "desvincular celular" o con `EXEC demo.sp_DesvincularDemo @Email = N'lucia@demo.edu';` |
| M15 | Apaga la API y pulsa "Escanear" | Mensaje "No hay conexión con el servidor…" sin cerrarse la app |
| M16 | **Salir**: cierra sesión | Vuelve al login. Al reabrir la app no entra sola |

En el emulador Android puedes fijar la ubicación desde *Extended controls → Location* para M10/M11. El emulador normalmente **no** marca esa ubicación como simulada, así que M13 solo se reproduce en un celular real.

## 5. Volver a empezar

```sql
-- Todo desde cero (fechas relativas a "ahora")
:r 03_datos_mock.sql      -- o ábrelo y ejecútalo

-- Solo liberar los celulares de Carlos, Lucía y Diego
EXEC demo.sp_DesvincularDemo;

-- Nuevo checkpoint en una sesión
EXEC demo.sp_AbrirCheckpoint @SesionId = <id>, @Minutos = 30;
```
