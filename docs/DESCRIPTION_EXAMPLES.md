# Description examples

## Window control

Before:

> Click the “Minimize” button to continue this task. This action is in File Explorer. The selected control is a button. Review the resulting screen before continuing to the next step.

After:

> This minimizes the active window to the taskbar without closing it, making other windows or applications accessible.

## Browser control not exposed through accessibility

Before:

> Click the “Chrome Legacy Window” pane. This action is in http://... The selected control is a pane. Review the resulting screen before continuing to the next step.

After:

> This activates the highlighted area shown in the captured browser screen. Windows did not expose a specific accessibility name for the clicked control, so the screenshot is the authoritative visual reference for this step.

If a vision-capable AI model identifies the visible control confidently, its useful description is preserved instead of being replaced by this fallback.