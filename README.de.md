# DayKeep

[简体中文](README.md) · [繁體中文](README.zh-TW.md) · [English](README.en.md) · [日本語](README.ja.md) · [한국어](README.ko.md) · [Français](README.fr.md) · [Deutsch](README.de.md) · [Español](README.es.md) · [Português](README.pt.md)

[Windows-ZIP herunterladen](https://github.com/Thicky-han/DayKeep/releases/download/v0.6.0-l6/DayKeep-0.6.0-l6-win-x64.zip) · [Quelltext](https://github.com/Thicky-han/DayKeep/releases/download/v0.6.0-l6/DayKeep-0.6.0-l6-source.zip) · [Prüfsummen](https://github.com/Thicky-han/DayKeep/releases/download/v0.6.0-l6/SHA256SUMS) · [Veröffentlichung](https://github.com/Thicky-han/DayKeep/releases/tag/v0.6.0-l6)

Jeden Tag ein Schritt, bis es erledigt ist. Lokale Windows-Erinnerungen für wiederholte Prüfungen, etwa ein noch geschlossenes Portal zur Patentgebührenzahlung. Heute geprüft bedeutet nicht vollständig erledigt. Vorabversion **0.6.0-l6**, mit Zustimmung veröffentlicht; neun Dokumentationssprachen, **die App-Oberfläche ist derzeit vereinfachtes Chinesisch**. Der dreitägige echte Nutzungstest steht aus. Keine Codesignatur.

![Hauptfenster mit erfundenen Aufgaben](assets/screenshots/main.png)

## Installation und Aktualisierung

DayKeep-0.6.0-l6-win-x64.zip vollständig entpacken. Windows x64, .NET-Laufzeit enthalten, kein SDK nötig. Nicht im ZIP starten oder nur die exe kopieren. Herkunft und SHA256SUMS prüfen; DayKeep.exe funktioniert portabel. Windows kann einen unbekannten Herausgeber melden.

Install.cmd installiert ohne Administratorrechte unter LocalApplicationData/Programs/DayKeep/0.6.0-l6 und erstellt DayKeep-Verknüpfungen auf Desktop/Startmenü. Die PowerShell-Richtlinienausnahme gilt nur für diesen Prozess. Autostart bleibt aus. Vor Updates Backup exportieren, Formulare speichern und die alte Version im Infobereich mit「退出日守」beenden; × blendet nur aus. Versionsordner und unbekannte Ordner/Verknüpfungen bleiben erhalten. Gleiche Version erneut installieren geht nur mit identischen, vollständigen Dateien. Erststart migriert die Datenbank transaktional auf schema 5; L1–L4 können sie danach nicht lesen.

## Erste Aufgabe

1. 「＋ 新建任务」: „Patentzahlung prüfen“ eingeben.「等待条件」für noch fehlende Voraussetzungen,「普通待办」für bereits ausführbare Aufgaben.
2. Optional vollständigen http/https-Link und Notizen eintragen. URLs mit Zugangsdaten werden abgelehnt; unbekannte Frist leer lassen.
3. `09:00` oder `09:00, 14:30`, 24-Stunden-Format HH:mm, höchstens acht verschiedene Zeiten; mindestens einen Wochentag wählen.
4. Standard: alle 30 Minuten zusätzlich, höchstens drei Extras je lokalem Tag. Intervall 5–1440 Minuten, Grenze 0–24. Null deaktiviert Extras, feste Zeiten bleiben.
5. Frist als `yyyy-MM-dd`. Heute fällige/überfällige Aufgaben werden hervorgehoben, nie automatisch erledigt. Speichern und im Infobereich weiterlaufen lassen.

## Täglicher Ablauf

| Chinesische Schaltfläche | Bedeutung |
| --- | --- |
| 打开平台 | Nur Browser öffnen, keine Prüfung oder Erledigung buchen |
| 今日已检查，尚未开放 | Heute geprüft, noch geschlossen: Prüfhinweise heute stoppen, am nächsten gewählten Tag fortsetzen |
| 已开放，转待办理 | Jetzt offen: zur Bearbeitung bereit, weiter verfolgen |
| 10 分钟后 / 30 分钟后 / 1 小时后 | Diese Aufgabe 10/30/60 Minuten verschieben; zwischenliegende Hinweise unterdrücken, Neustart überstehen |
| 确认已完成 | Nach tatsächlicher Bearbeitung erledigen; Hinweise stoppen, rückgängig möglich |

Gleichzeitige Aufgaben erscheinen in einem gemeinsamen, scrollbaren Vordergrundfenster ohne angeforderten Tastaturfokus und ohne sofortiges Verschwinden. Schließen erledigt nichts. Nach 09:00 folgen standardmäßig 09:30, 10:00, 10:30. Überschneidungen mit festen Zeiten werden zusammengefasst; Verschieben verbraucht kein Extra. Eine Tagesprüfung hebt die restlichen Warteprüfungen des Tages auf.

「暂停」pausiert ohne Zustandsverlust;「归档」archiviert ohne Löschen. Über Filter wiederherstellen.「撤销完成」stellt auch nach Neustart den früheren Zustand und die Pause wieder her.

## Desktop und Verlauf

「窄窗 / 展开」für schmal/breit,「置顶」für Anheften. Transparenz betrifft nur den Hintergrund. ×/Esc blendet aus; Ctrl+Alt+D, Symbolklick oder erneuter Start zeigt wieder an. Bei belegtem Hotkey das Symbol nutzen. Ausblenden bewahrt offene Formulare, Formularschließen verwirft Entwürfe.「退出日守」beendet wirklich. Ausgeschaltete Rechner/gestoppte Programme zeigen nichts an.

Bei Neustart/Aufwachen/Entsperren wird je Aufgabe nur der letzte gültige verpasste feste Termin zusammengefasst. Nicht weggeklickte Hinweise werden wiederhergestellt, weggeklickte Zeiten nicht wiederholt. Vollbild anderer Apps oder nicht verfügbare Desktops verzögern die Anzeige.「登录 Windows 后在托盘运行」ist standardmäßig aus; nach Ordnerwechsel erneut prüfen. Windows kann Autostart verzögern/deaktivieren.

「历史与备份」oder unten im schmalen Fenster「检查历史与备份」öffnen. Aufgabe einschließlich erledigter/archivierter, Datumsbereich inklusive beider Tage und Text aus Name/Aktion/Prüfnotiz wählen.「查询」sucht,「重置」setzt zurück, leer bedeutet unbegrenzt. Neueste 200 Treffer, für ältere eingrenzen; Backups enthalten alles. Aktueller Aufgabenname, ursprüngliches lokales Prüfdatum, Uhrzeit in aktueller Zeitzone.

## Backup, Wiederherstellung, Import zurücknehmen

1. 「备份与恢复 → 导出完整备份」in neue `.daykeep.json`. Kein Überschreiben; ungespeicherte Formulare ausgeschlossen.
2. Enthält Aufgaben, Verlauf, Erledigungsrücknahme, Archiv, Verschiebungen, Tagesbudgets und Zustellungen, nicht Darstellung/Autostart. Klartext privat verwahren; nur vertrauenswürdige Dateien importieren.
3. 「选择备份并预览」zeigt Datum/Zahlen ohne Änderung.「合并」ergänzt nur lokal fehlende GUIDs; gleiche IDs behalten die gesamte lokale Gruppe. Gleicher Name mit anderer ID bleibt getrennt.
4. 「替换」setzt auf das komplette Backup zurück und entfernt darin fehlende lokale Aufgaben. Formulare speichern/schließen, Folgen prüfen, Bestätigung anhaken. Moduswechsel entfernt den Haken.
5. Nach Daten-/Zustellungsänderungen「重新预览」. Vor dem Schreiben entsteht im Ordner Recovery neben tasks.db eine Sicherung der aktuellen Daten; bei Erfolg wird der Pfad gezeigt.
6. Zur Rücknahme diese Datei prüfen und ersetzen. Sicherungsfehler verhindert Änderungen; SQL-Fehler rollen zurück. Beschädigte, inkompatible oder ungültige Dateien/Prüfsummen werden abgelehnt.

Grenzen: 64 MiB, 20.000 Aufgaben, insgesamt 200.000 Datensätze. SHA-256 prüft Beschädigung, verschlüsselt/authentifiziert nicht. Wiederhergestellte offene Hinweise können sofort erscheinen.[Vertrag](docs/BACKUP_FORMAT.md).

## Daten, Deinstallation, Hilfe

LocalApplicationData/DayKeep enthält tasks.db und ui-settings.json. Programmwechsel entfernt sie nicht. Manuell erst beenden, dann ganzen Datenordner mit Begleitdateien kopieren. Defekte Datenbank vollständig sichern, ursprünglichen Ordner umbenennen, leere Datenbank neu öffnen und zuverlässiges Backup importieren.

Autostart abschalten, speichern, über Symbol beenden; **Uninstall.cmd im installierten Versionsordner** starten. Nur unveränderte aufgelistete Dateien/eigene Verknüpfungen werden entfernt, persönliche Daten und andere Versionen bleiben. Zusätzliche/geänderte Dateien stoppen die Deinstallation. Portable Nutzung braucht keinen Installer.

Keine Meldung: Symbol, Zeit/Tag, Pause/Archiv/Erledigung, Tagesprüfung und Vollbild prüfen. Alte Version sichtbar: dort wirklich beenden. Speicherfehler: Originale erhalten, Rechte/Platz/Sperren prüfen, Vorschau erneuern. Keine Konten, Telemetrie, Webseitenabfragen, Kennwörter oder automatischen Zahlungen.

Drei echte Nutzungstage, echter Ruhezustand/Sperrbildschirm, mehrere Monitore/gemischte DPI und exklusives Vollbild bleiben zu prüfen. Letzte reale L4-Minutenmessung wurde durch Desktopverfügbarkeit verzögert; Simulationen ersetzen diese Prüfung nicht.

[Testliste](docs/TRIAL_CHECKLIST.md) · [Chinesische Anleitung](INSTALL.md) · [Englische Entwicklungshinweise](README.en.md) · [Datenschutz](PRIVACY.md)

Originalcode, Dokumentation und bestätigtes Logo: AGPL-3.0-only. LICENSE und vollständigen korrespondierenden Quelltext mitliefern. Drittanbieter-Lizenzen im Paket unter licenses.[L6-Bericht](L6_REPORT.md)
