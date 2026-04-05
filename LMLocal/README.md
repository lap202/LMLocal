# LMLocal

LMLocal is a Visual Studio extension that integrates with LM Studio to provide a lightweight, local AI chat assistant directly inside the IDE.

## Preview Notice

> This extension is currently in preview. Features, behavior, UI, and documentation may change before the final release.

> This README is not final yet and may be updated as the extension evolves.

## Features

- Lightweight, in-IDE chat UI - Ask questions and get answers without leaving Visual Studio.
- Streaming responses - Partial output is delivered incrementally as the model generates it (token/line-level streaming). This gives low-latency feedback while the response is being produced, so you can see and act on partial results before the full reply completes.
- In-session chat history - Conversation history is kept in memory for the current Visual Studio session only. History is not persisted to disk and will be lost when Visual Studio is closed. (Persistent history across restarts is not currently implemented.)
- Markdown rendering and code highlighting - Replies with Markdown and fenced code blocks are rendered with proper formatting and syntax highlighting.
- Clipboard and quick-insert support - Easily copy or insert generated snippets into your code without sending data to external services.

## Requirements

- Visual Studio 2022
- LM Studio installed locally
- LM Studio local server enabled
- At least one chat-capable LLM loaded in LM Studio

LM Studio must have its local server enabled for the extension to connect. See the LM Studio server documentation for details: [LM Studio Server Documentation](https://lmstudio.ai/docs/developer/core/server)

## Installation

1. Install the VSIX package.
2. Restart Visual Studio if required.
3. Start LM Studio.
4. Enable the local server in LM Studio.
5. Load a chat-capable model in LM Studio.

## Getting Started

1. Open Visual Studio.
2. Open the LMLocal tool window from the extension command installed in Visual Studio.
3. Make sure LM Studio is running, the local server is enabled, and a model is loaded.
4. Wait for the extension to connect to LM Studio.
5. Confirm that the model name appears in the header.
6. Type a prompt and press `Enter` or click `Send`.
7. Use `Stop` to cancel generation if needed.
8. Use the trash button to clear the current chat history.

## Troubleshooting

### No model is shown

Make sure LM Studio is running and a model is loaded.

### The extension shows a connection error

- Verify that LM Studio is running.
- Verify that the local server is enabled in LM Studio.
- Make sure a compatible model is loaded.
- Restart LM Studio.
- Reload the tool window in Visual Studio.

### Responses do not stream

Make sure the selected model in LM Studio supports chat completions and is fully loaded.

## License

This project is licensed under the MIT License. See `LICENSE.txt` for details.


## Acknowledgments

Special thanks to the developers of LM Studio for providing the foundation for this extension, and to the open-source community for their ongoing support and contributions.

## Third-Party Components

This extension uses the following third-party client-side libraries:

- `marked` v15.0.12  Markdown parser
- `highlight.js` v11.9.0 and GitHub Dark theme (`github-dark.min.css`)  syntax highlighting library
