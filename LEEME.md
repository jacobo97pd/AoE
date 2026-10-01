# Emberfield para Codemagic y TestFlight

Este paquete contiene la exportación Xcode del juego Unity. No necesitas instalar
Unity ni activar una licencia Unity en Codemagic. La compilación nativa y la firma
se hacen en el Mac de Codemagic. El ZIP por sí solo no se instala en un iPhone.

## Primera prueba: comprobar la compilación

1. Descomprime el ZIP en una carpeta nueva. `codemagic.yaml`, `ios/` y `scripts/`
   deben quedar en la raíz de un repositorio Git independiente.
2. Instala Git LFS y ejecuta `git lfs install` **antes de añadir los archivos**.
   El `.gitattributes` incluido marca los datos y bibliotecas grandes. Añade
   explícitamente `ios`, `scripts`, `codemagic.yaml`, `.gitattributes`, `.gitignore`,
   `LEEME.md` y `package-manifest.json` a ese repositorio. Confirma y súbelo a tu
   proveedor Git; los archivos LFS también deben subirse. Este trabajo no ha hecho
   ningún push por ti.
3. En Codemagic, añade una aplicación conectando ese repositorio y selecciona
   configuración YAML. Elige el workflow `ios-compile-check` e inícialo manualmente.
4. Comprueba que termina correctamente y descarga `xcode-unsigned.log`.
   Esta opción no necesita certificados Apple y no produce una app instalable.

## Probar en iPhone o iPad con TestFlight

Necesitas una suscripción Apple Developer activa y acceso a App Store Connect.

1. Registra un Bundle ID propio y crea la ficha de Emberfield en App Store Connect.
2. El identificador definitivo del proyecto es `com.emberfield.jpedrero`; debe
   coincidir con el App ID que registres en Apple Developer.
   Está definido una sola vez, con un alias que comparten ambos workflows.
   `scripts/prepare_xcode.py` lo aplica al proyecto durante la compilación.
3. En las integraciones de tu equipo de Codemagic, configura una clave API de
   App Store Connect con el nombre **EmberfieldTestFlight**. Añade/genera el
   certificado Apple Distribution y el perfil App Store para el mismo Bundle ID
   en Code signing identities. Guarda las claves únicamente allí, nunca en Git.
4. Ejecuta manualmente `ios-testflight`. Genera la IPA firmada y la sube a App
   Store Connect. **No envía la app a revisión pública ni a revisión beta externa.**
5. Cuando Apple termine de procesarla, completa las preguntas que aparezcan
   (incluida la declaración de cifrado, si corresponde) y añade el build a un
   grupo de probadores internos en TestFlight. Instálalo desde la app TestFlight.

Los números de build son `BUILD_NUMBER + BUILD_NUMBER_OFFSET + 1`. Si el mismo
Bundle ID ya tiene builds subidos desde otro sistema, aumenta `BUILD_NUMBER_OFFSET`
en ambos workflows para superar el mayor número existente. La versión es 0.3.1.
Los workflows son manuales y utilizan Xcode 26.4, iOS físico ARM64 y Release.

## Alcance de esta entrega de prueba

- El paquete conserva el juego actual; no añade facciones ni modifica partidas.
- El icono de corona es provisional. La presentación de la tienda se prepara aparte.
- El servidor del proyecto apunta actualmente a `http://127.0.0.1:8787`.
  En un teléfono esa dirección es el propio teléfono: prueba inicialmente offline.
  Para online, vuelve a exportar con `-ServerUrl https://TU-SERVIDOR` y un servidor
  público compatible con la versión de contenido. Este ZIP no despliega un backend.
- Los comandos de voz de Windows no están disponibles en iOS.
- El rendimiento, la memoria y los controles táctiles deben comprobarse en dispositivos
  reales. Una exportación Xcode correcta no sustituye esa prueba ni la revisión Apple.
- Las modificaciones de código o contenido en Unity requieren volver a exportar.
  Para reproducir: `tools/package_ios.py prepare`, `tools/Export-iOS.ps1` y
  `tools/package_ios.py package` desde el repositorio de desarrollo.

El manifiesto del ZIP permite comprobar los archivos entregados antes de que el
script de Codemagic cambie el identificador y el número de build.

Documentación oficial: [añadir aplicaciones](https://docs.codemagic.io/getting-started/adding-apps/),
[firma iOS](https://docs.codemagic.io/yaml-code-signing/signing-ios/),
[publicación en App Store Connect](https://docs.codemagic.io/yaml-publishing/app-store-connect/).
