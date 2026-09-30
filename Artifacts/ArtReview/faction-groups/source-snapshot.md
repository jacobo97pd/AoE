# Fuentes de la entrega de facciones

La compilación usa una copia ligera de fuentes tomada el 14 de septiembre de 2026: **510 archivos, 2.999.117 bytes**. El [manifiesto](source-freeze-manifest.json) identifica sus rutas, tamaños y SHA-256.

La copia conserva facciones, red, modelos, integración de piratas y controles por voz. Excluye el cambio de idioma que estaba incompleto al capturar las fuentes. Solo dos archivos C# y tres archivos de ensamblado se adaptaron en la copia para retirar esa dependencia; el proyecto compartido conserva el trabajo paralelo, incluidos los catálogos de traducción incorporados después.

Por ello, esta entrega mantiene parte de la interfaz en inglés junto a los nombres nuevos en español. El arte y las cachés de Unity siguen enlazados; se trata de una copia de fuentes para compilar, no de un proyecto completo transportable.

El jugador Windows se ha compilado con Unity 6000.3.23f1, GUID `23bb857c791a4d0395f7a5a91dab4fd7`, sin errores ni advertencias de compilación. Pasaron las tres pruebas nativas HTTPS, cada una con dos clientes en este ordenador, reconexión e historial. También pasó la revisión funcional de las ocho facciones con 76 clics visibles. Las tres capturas del selector se inspeccionaron a 1280 × 720, con títulos y descripciones legibles y sin recortes. Véase el [informe técnico](../../../docs/technical/faction-realms-review.md) para las evidencias y los límites de estas pruebas.

El [paquete Windows](Emberfield-Windows.zip) ha pasado la verificación de CRC y SHA-256 en sus 181 entradas. La [evidencia de integridad](package-verification.json) es independiente de las pruebas nativas de juego y red.
