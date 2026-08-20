# Privacy and capture safety

SoplyraAI is designed as a local-first recorder.

- Screenshots and guide sessions are stored under `%LOCALAPPDATA%\SoplyraAI\Sessions`.
- The default description path uses Windows UI Automation metadata and local deterministic rules.
- Optional AI enhancement sends only step metadata (action, element label/control type, help text, and window title) to the configured endpoint.
- Screenshots are **not** sent to the AI endpoint by the current implementation.
- Password fields detected through UI Automation are masked in the captured screenshot.
- Typed characters are not recorded by the current implementation.
- The app skips its own UI and common Windows taskbar surfaces.

## Capture limitations

Windows may prevent low-privilege applications from inspecting or capturing an elevated/admin application. If a target application runs as administrator, SoplyraAI may need to run at the same integrity level.

Protected video/DRM surfaces, secure desktops, UAC prompts, and some GPU overlays may not be capturable.

Always review screenshots before sharing exported documentation.
