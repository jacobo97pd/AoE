# Paquete iOS para Codemagic — 29/09/2026

Se ha exportado Emberfield 0.3.0 a un proyecto Xcode con Unity 6000.3.23f1.
El resumen de Unity indica `Succeeded`, **0 errores y 0 avisos de build**.
Es una entrega para compilar, firmar y probar mediante TestFlight; no es una IPA
firmada ni acredita una compilación nativa o una prueba en dispositivo.

## Configuración y alcance

- Proyecto base: `ba3ae3f326e9edfe7d24bf21f56b141f83431742`.
- iPhone/iPad, ARM64, IL2CPP, Metal, iOS mínimo 15, orientación horizontal.
- Release, stripping Minimal y preservación explícita de DTOs serializables.
- Bundle ID provisional `com.emberfield.prototype`, versión 0.3.0, build inicial 1.
- Icono provisional opaco de 1024 px; iconos y launch screens presentes en Xcode.
- Manifiesto de privacidad de Unity referenciado desde UnityFramework.
- Cinco escenas existentes incluidas; ninguna modificación de reglas, facciones,
  simulación, mapas, arte del juego o versión de contenido.
- Firma, certificados y cuenta Apple se configuran en Codemagic. No se ha hecho
  push, subida a Apple, publicación ni ejecución remota de workflows.

El módulo oficial iOS de la misma revisión de Unity (`09d2ecc7fb28`) se incorporó
a la instalación local. La extracción de 3.435 archivos se validó con los CRC del
instalador y SHA-256 posterior. La compilación usó una copia materializada con
Library independiente, sin enlaces al proyecto de trabajo.

## Archivos de trabajo

- Entrega: `C:/Users/jacob/Downloads/Emberfield-iOS-Codemagic-0.3.0.zip`
  (1.092.011.256 bytes; 3.080 archivos más manifiesto; CRC verificado).
- SHA-256: `cc149cc369043a36f8461711e17f97299e82558e6971590b06b0c70ec8cf96ff`.
  Guardado también en el archivo `.zip.sha256` contiguo.
- Guía separada: `C:/Users/jacob/Downloads/Emberfield-iOS-Codemagic-LEEME.md`.
- Export: `D:/EmberfieldWorkingCache/ios-package-20260929/XcodeExport`.
- Copia Unity: `D:/EmberfieldWorkingCache/ios-package-20260929/UnityProject`.
- Log: `D:/EmberfieldWorkingCache/ios-package-20260929/unity-ios-export.log`.
- Resultados: `ios-exporter-tests.xml` y `xcode-portability-audit.json`, en la misma caché.
- Configuración entregable: `tools/ios/`, copiada a la raíz del ZIP.

La copia inicial registra 7.836 archivos y 1.682.509.189 bytes. Se corrigió después
un GUID mal formado de la nueva prueba Editor, registrado en `source-adjustments.json`.
La exportación Xcode contiene 3.075 archivos y 2.534.711.144 bytes antes del ZIP.
Los datos grandes y las bibliotecas están cubiertos por las reglas Git LFS.

## Verificación realizada

- 21 pruebas EditMode de `Emberfield.Tests.Editor.IosBuildToolsTests`: PASS.
- 8 pruebas Python de preparación para CI y detección de credenciales: PASS.
- YAML validado contra el esquema oficial de Codemagic: PASS.
- Cambio de Bundle ID y número de build probado sobre una copia del PBX/Info.plist
  realmente exportados, conservando la identidad de UnityFramework: PASS.
- Auditoría de rutas portables, herramientas nativas, Git LFS, iconos y privacidad:
  sin incidencias. Ninguna referencia absoluta de Windows en PBX/scripts.
- Hashes de los dos archivos excluidos y de ambos archivos de versión de contenido
  conservados. No se ha tocado `D:/EmberfieldJugar`.

El escáner distingue los identificadores públicos de ensamblados de Mono de las
credenciales. Su excepción solo permite elementos completos de mapas de clave
pública en `mono/**/machine.config`; continúa rechazando contraseñas y tokens
en el mismo archivo.
También se verificaron los 2.391 archivos de texto del export para evitar falsos
positivos con las licencias y los separadores de documentación de las bibliotecas.

## Repetir la exportación

Desde el repositorio, con todas las instancias de Unity cerradas y rutas nuevas:

```powershell
python -B tools/package_ios.py prepare --destination D:/EmberfieldWorkingCache/ios-next/UnityProject
./tools/Export-iOS.ps1 -SnapshotPath D:/EmberfieldWorkingCache/ios-next/UnityProject -OutputPath D:/EmberfieldWorkingCache/ios-next/XcodeExport
python -B tools/package_ios.py package --export D:/EmberfieldWorkingCache/ios-next/XcodeExport --support tools/ios --output D:/EmberfieldWorkingCache/ios-next/Emberfield-iOS.zip
```

Opcionales de `Export-iOS.ps1`: `-BundleId`, `-Version`, `-BuildNumber`,
`-ServerUrl https://servidor-publico`. La exportación no ejecuta los rebakes de
`ProjectTools.Verify` ni reconfigura el repositorio original. QualitySettings se
copia desde HEAD para preservar la modificación de usuario excluida.

## Codemagic y pruebas pendientes

El ZIP se descomprime en un repositorio Git independiente conectado a Codemagic;
no se carga como una aplicación instalable. `LEEME.md` explica la preparación con
Git LFS, el cambio de Bundle ID y los certificados.

`ios-compile-check` compila sin firma. `ios-testflight` genera la IPA firmada y
la sube a App Store Connect para asignarla a probadores internos; no solicita
revisión pública ni beta externa. Ambos son manuales y usan Xcode 26.4.

Pendientes: compilación nativa en ese Mac, firma Apple y prueba en iPhone/iPad
(arranque, controles táctiles, partidas, memoria y rendimiento). El endpoint
actual es localhost: la prueba inicial es offline. Online requiere reexportar
con HTTPS público y un servidor con la misma versión de contenido. El ZIP no
despliega el servidor. Los comandos de voz de Windows no están disponibles en iOS.

Fuentes oficiales: [Codemagic iOS](https://docs.codemagic.io/yaml-quick-start/building-a-native-ios-app/),
[firma](https://docs.codemagic.io/yaml-code-signing/signing-ios/),
[App Store Connect](https://docs.codemagic.io/yaml-publishing/app-store-connect/).
