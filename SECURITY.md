# Security Policy

SoplyraAI records desktop workflow context and screenshots, so privacy and secure handling are core product requirements.

## Reporting a vulnerability

Please do **not** publish credentials, private screenshots, exploit details, or other sensitive information in a public issue.

If the repository owner has GitHub private vulnerability reporting enabled, use that channel. Otherwise contact the repository owner privately through an available GitHub profile contact method and provide only the minimum information required to coordinate disclosure.

## Security-sensitive areas

Extra care is required for changes involving:

- screenshot capture/storage,
- password-field detection,
- clipboard/keyboard handling,
- local AI endpoints or API keys,
- file export paths,
- shell/process execution,
- installer/update behavior,
- future networking or synchronization.

## Default security posture

SoplyraAI is designed to keep sessions and screenshots local by default, avoid storing typed characters, mask detected password UI elements, and make AI enhancement optional.
