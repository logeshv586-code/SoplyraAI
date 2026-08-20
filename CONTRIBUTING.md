# Contributing to SoplyraAI

Thanks for helping improve SoplyraAI. The project is focused on reliable Windows workflow capture, privacy-aware documentation generation, and high-quality local-first UX.

## Development setup

Requirements:

- Windows 10/11
- .NET 8 SDK

```powershell
git clone https://github.com/logeshv586-code/SoplyraAI.git
cd SoplyraAI
dotnet restore
dotnet build SoplyraAI.sln
```

Run the app:

```powershell
dotnet run --project .\src\SoplyraAI.App\SoplyraAI.App.csproj
```

## Before submitting a change

1. Keep capture and AI responsibilities separated.
2. Do not introduce default cloud upload of screenshots.
3. Do not persist passwords, tokens, or raw typed secrets.
4. Preserve local-first operation when AI is disabled.
5. Add or extend self-test coverage for non-UI logic when practical.
6. Run a Windows Release build.
7. Manually test capture behavior if hooks, UI Automation, or screenshot code changed.

## Pull requests

Keep PRs focused and explain:

- what problem is being solved,
- what changed,
- how it was tested,
- any privacy/security implications,
- screenshots for visible UI changes.

## Good first contribution areas

- UI Automation metadata quality
- application/window edge cases
- screenshot redaction and annotation
- export formatting
- accessibility
- local model adapters
- documentation
