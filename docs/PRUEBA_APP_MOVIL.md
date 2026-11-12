# Probar la app móvil y generar la APK

## Cómo se conecta todo

```
App MAUI (celular)  --HTTP/JSON-->  Asistencia.Api (tu PC, puerto 5080)  --SQL-->  SQL Server (localhost\SQLEXPRESS)
```

- **La app nunca se conecta a SQL Server.** Solo llama a la API por HTTP (`/api/auth/login`, `/api/estudiante/marcar`, ...).
- **La API es la única que habla con SQL Server**, usando la cadena de `appsettings.Development.json`. Esa parte ya funciona, porque el portal web usa la misma API.
- Por eso, para que la app funcione basta con que **la API esté corriendo** y el celular pueda **llegar a tu PC por la red**.

Usuarios de prueba de la app (estudiantes): `lucia@demo.edu` o `diego@demo.edu`, contraseña `Demo123*`.

---

## Opción 1: emulador Android (lo más fácil)

1. En Visual Studio instala el workload **.NET Multi-platform App UI** (Visual Studio Installer > Modificar).
2. **Herramientas > Android > Android Device Manager** > crea un emulador (por ejemplo, Pixel con Android 14) e inícialo.
3. Ejecuta la API (perfil **http**).
4. Clic derecho en la solución > **Configurar proyectos de inicio** > *Varios proyectos de inicio*: `Asistencia.Api` y `Asistencia.Maui` en **Iniciar**.
5. En la barra superior elige el framework **net10.0-android** y el emulador, y pulsa **F5**.

No hay que cambiar nada: en el emulador la app usa `http://10.0.2.2:5080/`, que es la dirección de tu PC vista desde el emulador.

> Para escanear el QR desde el emulador, en la configuración de la cámara del emulador elige *Webcam0* (la cámara de tu PC) y apunta a la pantalla de proyección. Si no, usa **Ingresar código** con los 6 dígitos.

---

## Opción 2: celular físico conectado por cable

1. En el celular: **Ajustes > Acerca del teléfono** > toca 7 veces *Número de compilación*, luego activa **Depuración USB** en *Opciones de desarrollador*.
2. Conecta el celular por USB. Visual Studio lo muestra en la lista de dispositivos.
3. **Celular y PC en la misma red WiFi.**
4. Busca la IP de tu PC: en CMD ejecuta `ipconfig` y copia la *Dirección IPv4* del adaptador WiFi/Ethernet (por ejemplo, `192.168.1.50`).
5. Abre el puerto 5080 en el firewall (CMD como administrador):
   ```
   netsh advfirewall firewall add rule name="Asistencia API 5080" dir=in action=allow protocol=TCP localport=5080
   ```
6. Comprueba desde el navegador del celular: `http://<IP-de-tu-PC>:5080/Cuenta/Login` debe abrir el portal.
7. Configura la IP en la app (dos archivos):
   - `src/Asistencia.Maui/Constantes.cs`: cambia `"http://localhost:5080/"` por `"http://<IP-de-tu-PC>:5080/"`.
   - `src/Asistencia.Maui/Platforms/Android/Resources/xml/network_security_config.xml`: agrega `<domain includeSubdomains="false"><IP-de-tu-PC></domain>`.
8. Ejecuta con **F5** eligiendo el celular.

---

## Generar la APK para instalar sin cable

1. Haz los pasos 3 a 7 de la opción 2. **Importante:** en Release, `Constantes.cs` usa la URL de producción (`https://asistencia.tuinstitucion.edu.ec/`), que todavía no existe. Para una APK de prueba, pon también ahí la IP de tu PC.
2. En Visual Studio elige configuración **Release** y framework **net10.0-android**.
3. Clic derecho en `Asistencia.Maui` > **Publicar...** > **Ad Hoc** > crea una identidad de firma (keystore) con una contraseña y **guárdala**: la necesitarás para cada actualización.
4. Pulsa **Distribuir** > **Guardar como**. Obtienes `com.tuinstitucion.asistencia-Signed.apk`.
5. Copia la APK al celular (cable, Drive, WhatsApp) y ábrela. Android pedirá permitir *instalar apps de origen desconocido*.

Alternativa por consola (desde la carpeta `AsistenciaApp`):
```
dotnet publish src/Asistencia.Maui/Asistencia.Maui.csproj -f net10.0-android -c Release -p:AndroidPackageFormat=apk
```
La APK queda en `src/Asistencia.Maui/bin/Release/net10.0-android/publish/`. Sin keystore propio, se firma con la clave de depuración, que sirve para pruebas pero no para publicar en Play Store.

---

## Problemas comunes

| Síntoma en la app | Causa probable |
|---|---|
| "No se pudo conectar" / tiempo de espera | API apagada, firewall, celular en otra red o IP mal escrita |
| `Cleartext HTTP traffic ... not permitted` | Falta la IP en `network_security_config.xml` |
| `DISPOSITIVO_NO_VINCULADO` | El estudiante ya entró desde otro celular: en el portal, **desvincular celular** |
| `CHECKPOINT_CERRADO` | No hay checkpoint abierto: lánzalo desde el detalle de la sesión en el portal |
| La IP de la PC cambió | Pasa con el DHCP del WiFi: repite el paso 4 o fija una IP en el router |
