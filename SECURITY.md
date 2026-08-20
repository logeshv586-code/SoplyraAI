# Security Policy

SoplyraAI records desktop workflow context and screenshots, so privacy and secure handling are product requirements rather than optional add-ons.

## Security invariants

The project is intended to preserve these properties:

- SoplyraAI does **not** install a keyboard hook and does not record typed characters.
- Screenshots and sessions stay on the local machine unless the user deliberately exports or shares them.
- The current AI enhancement path sends **step metadata, not screenshots**.
- Password-field steps are redacted locally and are not sent to an AI endpoint.
- Remote AI endpoints are disabled by default. When explicitly enabled, only HTTPS remote endpoints are accepted.
- AI API keys are protected with Windows DPAPI for the current Windows user instead of being stored as plaintext JSON.
- Loaded session files are treated as untrusted data. Session paths and screenshot paths must resolve inside SoplyraAI's own session directory.
- Helper executables are launched only from expected absolute paths; automatic AI setup refuses to run from an elevated SoplyraAI process.
- The application requests `asInvoker` privileges and `uiAccess=false`.

## Reporting a vulnerability

Please do **not** publish credentials, private screenshots, exploit details, or other sensitive information in a public issue.

If GitHub private vulnerability reporting is enabled for this repository, use that channel. Otherwise contact the repository owner privately through an available GitHub profile contact method and provide only the minimum information required to coordinate disclosure.

Useful reports include:

- affected commit/version,
- security boundary involved,
- minimal reproduction steps,
- impact,
- whether sensitive data was exposed,
- suggested mitigation if known.

## Security-sensitive areas

Extra review is required for changes involving:

- screenshot capture/storage/redaction,
- password-field detection,
- keyboard or clipboard handling,
- local/remote AI endpoints or API keys,
- session deserialization,
- file export paths,
- shell/process execution,
- installer/update behavior,
- GitHub Actions and release generation,
- future networking or synchronization.

## Build and release security

The repository includes:

- Windows Release build and executable self-tests,
- dependency vulnerability reporting,
- CodeQL analysis,
- Dependabot for NuGet and GitHub Actions,
- pinned Inno Setup package version in CI,
- SHA-256 checksums for generated Windows release artifacts,
- least-privilege GitHub Actions permissions.

Release artifacts are not code-signed unless a future release process explicitly adds a trusted signing certificate. Users should verify checksums from the matching GitHub Actions artifact/release and expect Windows SmartScreen warnings for unsigned binaries.

## Residual privacy risk

A screenshot can still contain sensitive information outside the clicked control. SoplyraAI masks detected password controls, but it cannot guarantee that every secret, personal value, protected application surface, or unrelated window content is automatically detected.

Always review generated screenshots before sharing or exporting documentation.
