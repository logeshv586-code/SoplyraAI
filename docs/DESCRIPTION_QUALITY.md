# Step description quality

SoplyraAI uses Windows UI Automation first and optional AI/vision second.

When Windows exposes a meaningful control name such as **Save**, **Submit**, **Minimize**, or **Search**, SoplyraAI generates a purpose-specific description.

When a browser exposes only a generic accessibility surface such as `Chrome Legacy Window`, SoplyraAI does not pretend that this is the real clicked control. The step is labeled **highlighted area**, the screenshot is treated as the authoritative visual reference, and the description clearly states the accessibility limitation.

Legacy boilerplate such as `This action is in ... The selected control is a pane. Review the resulting screen...` is normalized automatically when a saved guide is loaded or exported. Useful user-edited or AI-generated descriptions are preserved.