# Research notes and product decisions

SoplyraAI is an original implementation informed by public product behavior and open-source projects. No source code was copied from the projects below.

## Scribe product behavior studied

Public Scribe documentation describes a recorder that starts capture, records a screenshot and text instruction for clicks/keystrokes, then lets users edit, redact/customize, and export/share guides.

## AWS sample-scribe-ai

Repository: https://github.com/aws-samples/sample-scribe-ai

Useful for: knowledge capture, AI document generation, semantic knowledge bases, and human-in-the-loop review.

Not used as the desktop recorder core because it is an AWS/Bedrock knowledge-interview application rather than a click-to-screenshot Windows recorder.

## OpenSteps

Repository: https://github.com/ebanez8/openstep
License: MIT

Useful concepts: Windows-native global click capture, UI Automation context, local sessions, active-window/full-desktop screenshots, and portable exports.

## Mimik

Repository: https://github.com/westpoint-io/mimik
License: MIT

Useful concepts: structured interaction context, annotated screenshots, smart privacy, optional AI descriptions from lightweight metadata rather than vision images, and client-side export.

## Microsoft Skill Recorder

Repository: https://github.com/microsoft/skill-recorder
License: MIT

Useful concepts: always-on-top recording control, local capture before analysis, event timelines, and converting recorded workflows into higher-level reusable instructions.

## Architecture choice

For a Windows EXE, WPF/.NET 8 is intentionally used for the first version because it provides direct access to Windows hooks, UI Automation, desktop capture APIs, and simple self-contained x64 publishing without requiring a browser extension.
