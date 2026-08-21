# SoplyraAI Threat Model

This document describes the main security boundaries for the Windows-first workflow recorder.

## Assets

The most sensitive assets are:

1. captured screenshots,
2. UI metadata such as element labels and window titles,
3. locally stored guide/session files,
4. optional AI API credentials,
5. generated documentation exports,
6. release/build integrity.

## Trust boundaries

### Windows desktop → SoplyraAI capture process

Mouse-down events, UI Automation metadata, foreground-window information, and pixels are external inputs. Applications can expose misleading, malformed, very large, or sensitive UI metadata.

**Controls**

- no keyboard hook,
- bounded/sanitized UI metadata,
- password-field flag handling,
- own-process and shell-surface exclusion,
- screenshot storage only under the trusted session folder.

### Session JSON → application runtime

Files under `%LOCALAPPDATA%\SoplyraAI\Sessions` can be edited by the current user or another process running as that user. Deserialized values are therefore untrusted.

**Controls**

- session directory derived from the session GUID,
- GUID folder and JSON identity must match,
- top-level session enumeration only,
- reparse-point checks,
- file-size and step-count limits,
- screenshot path canonicalization,
- PNG signature/size validation before reads or exports,
- text/context sanitization after load.

### SoplyraAI → AI endpoint

Step metadata can include sensitive business information. The AI boundary must not silently turn a local-first recorder into a network exfiltration path.

**Controls**

- local loopback endpoint is the default,
- remote endpoints require explicit user opt-in,
- remote endpoints require HTTPS,
- URL credentials/query strings/fragments are rejected,
- redirects are disabled,
- cookies/default Windows credentials are disabled,
- loopback requests bypass the system proxy,
- screenshots are not included,
- password-field steps are not sent,
- request/response sizes and response time are bounded,
- model output is treated as untrusted text and sanitized.

### Settings → AI credentials

The settings file is ordinary local application data and should not expose a reusable API credential at rest.

**Controls**

- API keys are excluded from normal JSON serialization,
- secrets are encrypted with Windows DPAPI `CurrentUser`,
- legacy plaintext keys are migrated on load,
- encryption failures fail closed by not persisting plaintext.

### SoplyraAI → helper executables

The application may install Ollama or download a local model. PATH-based executable resolution can become code execution when the process is elevated.

**Controls**

- no `where.exe` / `Get-Command` trust for helper discovery,
- expected absolute executable paths only,
- automatic setup refuses to run while SoplyraAI is elevated,
- helper arguments use structured argument lists,
- helper processes have timeouts and are killed on timeout.

### Guide → exported HTML/Markdown/PDF

Guide text can originate from UI Automation, local files, or an AI model and is not trusted markup.

**Controls**

- HTML encoding,
- restrictive Content Security Policy,
- no-referrer policy,
- Markdown metacharacter escaping,
- only validated session PNGs are exported,
- Edge PDF conversion runs with background networking/extensions disabled and a timeout.

### GitHub source → Windows release

A compromised dependency, workflow, or build helper can affect distributed binaries.

**Controls**

- minimal workflow token permissions,
- checkout does not persist repository credentials,
- deterministic .NET SDK selection,
- dependency vulnerability reporting,
- CodeQL,
- Dependabot,
- pinned Inno Setup package version,
- executable self-test before installer packaging,
- SHA-256 artifact checksums.

## Attacker assumptions

We defend against:

- malicious or malformed application UI metadata,
- accidental remote AI configuration,
- tampered session/settings files,
- path traversal and local reparse-point abuse,
- untrusted AI output,
- PATH/executable-search hijacking,
- common CI permission and dependency risks.

A process already running as the same Windows user can generally read the same `%LOCALAPPDATA%` screenshots and interact with the desktop. SoplyraAI cannot create a strong isolation boundary against a fully compromised user account; operating-system account security remains required.

## Non-goals and residual risks

- automatic detection of every secret visible anywhere in a screenshot,
- capturing protected DRM/secure-desktop/UAC surfaces,
- defeating malware already running with equal or higher user privileges,
- code-signing without a configured signing identity,
- enterprise key management or centrally managed retention,
- sandboxing third-party local AI runtimes.

These can become separate hardening projects if SoplyraAI moves from a local-first desktop utility toward enterprise deployment.
