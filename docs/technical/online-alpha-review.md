# Alfa online: abrir y verificar

El servidor autoritativo, las cuentas, las salas, la reconexión y el historial ya se han probado con dos ejecutables nativos de Unity mediante HTTPS público. Los dos procesos se ejecutaron en este mismo PC; esto no acredita dos ordenadores o redes físicas distintas.

## Jugar

Abrir `Builds/Windows/Emberfield.exe`, entrar en **MULTIPLAYER**, comprobar conexión y crear una cuenta o iniciar sesión. Un jugador crea una sala privada y comparte el código; el otro entra con su propia cuenta. Ambos eligen facción y marcan que están listos. El servidor incluido en el ejecutable es la dirección temporal válida al compilar.

La dirección vigente también se guarda en `D:/CodexTooling/online-validation/alpha/state.json`. Este comando comprueba el endpoint y pasa la dirección actual al juego, incluso si el túnel cambió desde la última compilación:

```powershell
./tools/Play-OnlineAlpha.ps1
```

La sesión pública depende de que este PC, el proceso Node, los workers C# y `cloudflared` sigan en marcha. No es un alojamiento permanente. Cloudflare describe los Quick Tunnels como una herramienta de pruebas, sin garantía de disponibilidad y con un límite de 200 peticiones en vuelo; la prueba de carga masiva se realizó contra el servicio local, no contra ese túnel. [Documentación de Quick Tunnels](https://developers.cloudflare.com/cloudflare-one/networks/connectors/cloudflare-tunnel/do-more-with-tunnels/trycloudflare/).

## Control del servicio

```powershell
./tools/Start-OnlineAlpha.ps1 -Public
./tools/Stop-OnlineAlpha.ps1
```

Los scripts guardan identidad de procesos, horas de arranque y logs en `D:/CodexTooling/online-validation/alpha`. Solo paran los procesos administrados que todavía coincidan con esa identidad. La base de datos persistente está en `D:/EmberfieldOnline/data/alpha.sqlite`; detener o reiniciar conserva cuentas y resultados. Reiniciar el túnel puede producir otra URL. Después debe usarse el lanzador anterior, introducir la dirección nueva en el cliente o recompilar el recurso de servidor.

Fuera de loopback el cliente exige HTTPS válido. No se desactiva la validación de certificados. Solo la instancia configurada con `-Public` confía en la cabecera de IP de Cloudflare recibida desde loopback; esa opción permite aplicar cuotas de autenticación por visitante detrás del túnel.

## Evidencia

- **Compilación final con los 26 personajes:** `05a91cf2371d432293b419110de45b34`. Se ejecutaron simultáneamente dos pruebas, con cuatro procesos de Unity: histórica en Amber Crossing (**36,58 s, PASS**) y fantasía en Sapphire Coast (**35,40 s, PASS**). Ambas verificaron conexión, movimiento, entrenamiento, entrega, cierre forzado y reinicio del invitado, reconexión al mismo puesto, resultado, historial y conservación de selección al volver al lobby. Sus informes son `TestResults/Online-HTTPS-Historical-Collection-Final-20260913/summary.json` y `TestResults/Online-HTTPS-Fantasy-Collection-Final-20260913/summary.json`.
- `TestResults/Online-HTTPS-Historical-Fixed-20260913/summary.json`: partida histórica en Amber Crossing, dos ejecutables, movimiento, entrenamiento, entrega, cierre forzado/reinicio del invitado, mismo puesto, resultado e historial, vuelta al lobby.
- `TestResults/Online-HTTPS-Fantasy-20260913/summary.json`: mismo recorrido en fantasía, Sapphire Coast.
- `TestResults/Online-HTTPS-Historical-20260913/summary.json`: primer fallo conservado. Descubrió el atasco del recolector cuando una unidad recién entrenada ocupaba el punto de descarga.
- `docs/technical/online-load-report.md`: carga local de 32/64 partidas, métricas y límites, incluida una conexión fallida durante el cierre masivo que se conserva en el informe.
- `D:/CodexTooling/online-validation/dropoff-probe/`: regresión que falla con el código anterior y entrega con el nuevo, sin teletransporte ni ignorar colisiones.

El arreglo de descarga replanifica un acceso ocupado tras un bloqueo acotado. Cinco regresiones nuevas y las 53 pruebas de economía existentes pasan. Perfil e historial tienen un único reintento ante fallos transitorios de transporte; los POST y las órdenes de juego no se repiten automáticamente. La verificación del cliente rechaza incompatibilidades de versión y respuestas de sesiones anteriores.

La carga mide partidas iniciales durante decenas de segundos. No demuestra estabilidad de varias horas, grandes ejércitos tardíos, rendimiento móvil ni disponibilidad de producción.

El ZIP `Artifacts/ArtReview/reference-characters/Emberfield-Windows.zip` contiene el jugador completo para Windows; hay que extraer la carpeta entera. `PERSONAJES` abre la revisión de modelos y `MULTIPLAYER` el acceso al servidor. La dirección temporal incluida es `https://characteristics-advocate-bass-paul.trycloudflare.com`; el lanzador del proyecto permite usar la dirección vigente si el túnel se reinicia.
