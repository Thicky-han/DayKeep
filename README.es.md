# DayKeep

[简体中文](README.md) · [繁體中文](README.zh-TW.md) · [English](README.en.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Português](README.pt.md)

[Descargar ZIP de Windows](https://github.com/Thicky-han/DayKeep/releases/download/v0.7.0-beta.1/DayKeep-0.7.0-beta.1-win-x64.zip) · [Código fuente](https://github.com/Thicky-han/DayKeep/releases/download/v0.7.0-beta.1/DayKeep-0.7.0-beta.1-source.zip) · [Sumas de comprobación](https://github.com/Thicky-han/DayKeep/releases/download/v0.7.0-beta.1/SHA256SUMS) · [Publicación](https://github.com/Thicky-han/DayKeep/releases/tag/v0.7.0-beta.1)

Un paso cada día, hasta terminar. Recordatorios locales para Windows de tareas que requieren comprobaciones, como un portal de pago de patentes aún cerrado. Comprobar hoy no completa toda la tarea. La versión preliminar pública actual **0.7.0-beta.1** cuenta con la aprobación del responsable y añade nueve idiomas de interfaz y un selector visible. Los enlaces de arriba ofrecen esta versión, sus fuentes y sus sumas; los resultados de compilación, empaquetado y el avance de las verificaciones públicas figuran en el [informe de actualización de idiomas](LANGUAGE_UPDATE_REPORT.md). La anterior **0.6.0-l6** solo tiene interfaz en chino simplificado; sus adjuntos y etiqueta originales no cambian. Los documentos congelados del paquete aprobado reflejan su estado de candidata al empaquetarlo. La prueba real de tres días sigue pendiente. Binarios sin firma.

![Ventana con tareas ficticias](assets/screenshots/main.png)

## Cambiar el idioma de la interfaz

Abre **Language / 语言** en la parte superior de la ventana principal. El selector permanece visible tanto en modo amplio como compacto, sin entrar en ajustes. Cada idioma aparece con su propio nombre: **简体中文, 繁體中文, English, 日本語, 한국어, Français, Deutsch, Español, Português**.

La elección actualiza al instante los textos de la ventana principal, formularios, recordatorios, historial y copias. Se guarda en este equipo y se recupera al iniciar de nuevo. Sin una elección guardada, también al actualizar, se usa chino simplificado. Si falla el guardado, aparece un error y se conserva el idioma anterior.

Tus nombres de tareas, notas, enlaces y registros originales no se traducen. El cambio no modifica la zona horaria local del equipo, los días ni las horas programadas. Usa `HH:mm` de 24 horas y `yyyy-MM-dd` para fechas límite. Los botones chinos de abajo sirven como referencia; la app muestra su equivalente en el idioma elegido. [Idiomas y guardado](docs/LANGUAGES.md)

## Instalar y actualizar

Extrae todo DayKeep-0.7.0-beta.1-win-x64.zip. El ZIP anterior 0.6.0-l6 se usa igual, pero no tiene selector de idioma. Windows x64, entorno .NET incluido, sin SDK. No ejecutes dentro del ZIP ni copies solo el exe. Comprueba origen y SHA256SUMS; abre DayKeep.exe para uso portátil. Windows puede mostrar un editor desconocido.

Install.cmd instala sin administrador en LocalApplicationData/Programs/DayKeep/0.7.0-beta.1 y crea accesos DayKeep en escritorio/Inicio. La excepción de política PowerShell solo afecta a ese proceso. No activa el inicio de sesión. Antes de actualizar, exporta copia, guarda formularios y sal de la versión anterior con「退出日守」en la bandeja; × solo oculta. Cada versión conserva su carpeta; las carpetas/accesos desconocidos no se sobrescriben. Reinstalar la misma versión exige archivos idénticos e intactos. El primer inicio migra mediante transacción a schema 5, que L1–L4 no pueden leer.

## Crear la primera tarea

1. 「＋ 新建任务」: escribe “Revisar pago de patente”.「等待条件」si falta una condición;「普通待办」si ya puedes actuar.
2. URL completa http/https y notas son opcionales. No se aceptan credenciales en URL. Deja vacía una fecha límite desconocida.
3. `09:00` o `09:00, 14:30`, HH:mm de 24 horas, hasta ocho horas distintas; selecciona al menos un día.
4. Por defecto añade cada 30 minutos, máximo tres extras por día local. Intervalo 5–1440 minutos, límite 0–24. Cero desactiva extras, mantiene horas fijas.
5. Fecha conocida: `yyyy-MM-dd`. Vence hoy/atrasada se resalta y prioriza, nunca se completa automáticamente. Guarda y mantén la app en la bandeja.

## Seguimiento diario

| Botón (referencia china) | Efecto |
| --- | --- |
| 打开平台 | Solo abre navegador, no registra comprobación ni finalización |
| 今日已检查，尚未开放 | Comprobado, sigue cerrado: detiene comprobaciones de hoy; vuelve el próximo día elegido |
| 已开放，转待办理 | Disponible: pasa a pendiente de gestión y continúa seguimiento |
| 10 分钟后 / 30 分钟后 / 1 小时后 | Posponer esta tarea 10/30/60 minutos; sustituye avisos intermedios y persiste tras reinicio |
| 确认已完成 | Confirmar cuando realmente termines; detiene avisos, permite deshacer |

Las tareas simultáneas se agrupan en una ventana superior con desplazamiento, sin pedir foco de teclado ni desaparecer a los pocos segundos. Cerrarla no completa nada. Tras 09:00, los extras habituales son 09:30, 10:00 y 10:30. Un cruce con hora fija se agrupa sin gastar extra; posponer tampoco consume el cupo. La comprobación diaria cancela los avisos de espera restantes de ese día.

「暂停」pausa sin perder estado;「归档」archiva sin borrar. Recupera mediante filtros.「撤销完成」restaura estado y pausa anteriores incluso después de reiniciar.

## Escritorio e historial

「窄窗 / 展开」cambia anchura;「置顶」fija arriba. Solo el fondo se hace transparente. ×/Esc oculta; Ctrl+Alt+D, clic en bandeja o nuevo inicio recupera. Si el atajo está ocupado, usa la bandeja. Ocultar conserva formularios; cerrarlos descarta borradores.「退出日守」sale completamente. No hay avisos con app detenida o equipo apagado.

Reinicio, reanudación o desbloqueo recupera solo el último horario fijo perdido válido por tarea. Avisos no recogidos vuelven; los recogidos no se duplican. Pantalla completa de otra app o escritorio no disponible aplaza la visualización.「登录 Windows 后在托盘运行」está desactivado por defecto; revisa tras cambiar carpeta. Windows puede desactivar/retrasar ese inicio.

Abre「历史与备份」o abajo en modo compacto「检查历史与备份」. Elige tarea, incluso terminada/archivada, fechas inclusivas y texto de nombre/acción/nota;「查询」consulta,「重置」restablece. Vacío = sin límite. Muestra los últimos 200 resultados; acota para los anteriores. La copia incluye todo. Nombre actual, fecha local original de comprobación, hora en zona actual.

## Copias, restauración y deshacer importación

1. 「备份与恢复 → 导出完整备份」a un nuevo `.daykeep.json`. Sin sobrescribir; excluye formularios sin guardar.
2. Incluye tareas, historial, deshacer finalización, archivo, posposición, cupos diarios y entregas; no idioma/apariencia/inicio. Importar no cambia el idioma elegido. Es texto sin cifrar: protégelo e importa solo fuentes fiables.
3. 「选择备份并预览」enseña fechas/cantidades sin cambios.「合并」añade solo GUID ausentes; conserva íntegro el grupo local del mismo ID. Mismo nombre con ID distinto sigue separado.
4. 「替换」restaura toda la copia y elimina tareas locales ausentes. Guarda/cierra formularios, revisa impacto y marca confirmación. Cambiar modo o idioma desmarca. Al cambiar idioma se conservan archivo, modo y vista previa congelada; revisa el impacto en el nuevo idioma y confirma otra vez.
5. Si cambian datos/avisos,「重新预览」. Antes de escribir se guarda el estado actual en Recovery junto a tasks.db y se muestra la ruta al terminar.
6. Para deshacer, previsualiza ese archivo y reemplaza. Si falla la copia de recuperación no se cambia la base; errores SQL revierten la transacción. Se rechaza daño, incompatibilidad, estructura o suma incorrecta.

Límites: 64 MiB, 20.000 tareas, 200.000 registros totales. SHA-256 detecta daños, no autentica ni cifra. Los avisos recuperados pendientes pueden aparecer inmediatamente.[Contrato](docs/BACKUP_FORMAT.md).

## Datos, desinstalación y ayuda

LocalApplicationData/DayKeep guarda tasks.db y ui-settings.json. Cambiar programa no elimina datos. Para copiar manualmente, sal y copia toda la carpeta con archivos auxiliares. Si está dañada, conserva copia completa, renombra original, abre base vacía e importa copia fiable.

Desactiva inicio de sesión, guarda y sal de bandeja; ejecuta **Uninstall.cmd de la carpeta instalada**. Retira solo archivos intactos de su lista y accesos que apuntan a esa versión; conserva datos y otras versiones. Archivos extra/modificados detienen el borrado. El portátil no requiere desinstalador.

Sin aviso: revisa bandeja, hora/día, pausa/archivo/completado, comprobación de hoy y pantalla completa. Si abre versión anterior, sal de ella. Error al guardar/importar: conserva archivos, revisa permisos/espacio/bloqueo y renueva vista previa. Sin cuentas, telemetría, lectura web, contraseñas ni pagos automáticos.

Tres días reales, suspensión/bloqueo reales, varios monitores/DPI mixtos y pantalla completa exclusiva siguen pendientes. La última observación real L4 se aplazó por disponibilidad del escritorio; las simulaciones no sustituyen estas pruebas.

[Lista de prueba](docs/TRIAL_CHECKLIST.md) · [Guía china](INSTALL.md) · [Desarrollo en inglés](README.en.md) · [Privacidad](PRIVACY.md)

Código, documentos y logo aprobado: AGPL-3.0-only. Distribuye LICENSE y código fuente completo correspondiente. Licencias de terceros en licenses del paquete.[Informe L6](L6_REPORT.md)
