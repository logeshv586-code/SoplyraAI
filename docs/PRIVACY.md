# Privacy and capture safety

SoplyraAI is designed as a local-first recorder.

- Screenshots and guide sessions are stored under `%LOCALAPPDATA%\SoplyraAI\Sessions`.
- The default description path uses Windows UI Automation metadata and local deterministic rules.
- The global hook records mouse-down actions only; SoplyraAI does not install a keyboard hook or store typed characters.
- Screenshots are **not** sent to the AI endpoint by the current implementation.
- Password fields detected through UI Automation are masked in the captured screenshot.
- Password-field steps are not sent to AI enhancement.
- UI metadata is bounded, control characters are removed, and several high-confidence credential/token formats are redacted before persistence or AI use.
- The app skips its own UI and common Windows taskbar surfaces.

## AI privacy boundary

The default AI endpoint is loopback-only:

`http://127.0.0.1:11434/v1`

Remote endpoints are disabled unless the user explicitly enables **Allow remote HTTPS AI endpoints**. When enabled:

- only HTTPS remote URLs are accepted,
- URL-embedded credentials, query strings, and fragments are rejected,
- redirects are disabled,
- screenshots are still not sent,
- the request contains only the metadata required to rewrite the selected procedure step.

AI API keys are encrypted at rest with Windows DPAPI for the current user.

## Local file boundary

Session JSON is treated as untrusted input when reopened. SoplyraAI reconstructs each session folder from its GUID and validates screenshot paths before reading, copying, or embedding images into an export. Reparse-point/junction checks, size limits, PNG signature checks, and step-count limits reduce local tampering risk.

## Capture limitations

Windows may prevent low-privilege applications from inspecting or capturing an elevated/admin application. Running SoplyraAI at the same integrity level may be required for those targets, but automatic local-AI installation is intentionally disabled while SoplyraAI itself is elevated.

Protected video/DRM surfaces, secure desktops, UAC prompts, and some GPU overlays may not be capturable.

A screenshot can still contain sensitive information outside the clicked control. Always review screenshots before sharing or exporting documentation.
