# CONTRIBUTING.md

This repository coding conventions:

- Modules should avoid using `this` in arrow functions when returning module-local API objects.
- Prefer returning a local `api` object from factory functions and use classic function syntax for methods that rely on dynamic `this` semantics.
- Keep single source of truth for shared constants in `js/app-globals.js`.
- Tests (Playwright) expect module scripts to be importable via `https://app.local/js/...` mappings done by the test fixture.

These guidelines are applied automatically when contributing.