# LMLocal.Tests.E2E

E2E tests for `app.html` using [Playwright](https://playwright.dev/dotnet/) + MSTest.

## Requirements

- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [PowerShell 7+](https://github.com/PowerShell/PowerShell/releases) (`pwsh`)

## First Run

### 1. Build the project

```powershell
cd D:\Work\lmlocal
dotnet build LMLocal.Tests.E2E\LMLocal.Tests.E2E.csproj
```

### 2. Install Playwright browsers (once)

**Option A — PowerShell 7+ (`pwsh`)**
```powershell
pwsh LMLocal.Tests.E2E\bin\Debug\net9.0\playwright.ps1 install chromium
```

**Option B — Windows CMD (no PowerShell required)**
```cmd
cd D:\Work\lmlocal
dotnet build LMLocal.Tests.E2E\LMLocal.Tests.E2E.csproj
dotnet tool install --global Microsoft.Playwright.CLI
playwright install chromium
```

> Browsers are installed into the user profile and do not need to be reinstalled.

### 3. Run the tests

#### Via Visual Studio

1. `Test → Test Explorer` (`Ctrl+E, T`)
2. Click **Run All** or right-click a specific test → **Run**

#### Via command line

```powershell
cd D:\Work\lmlocal
dotnet test LMLocal.Tests.E2E\LMLocal.Tests.E2E.csproj
```

#### With visible browser (headed mode)

By default Playwright runs headless. To see the browser during a test run:

**PowerShell:**
```powershell
$env:HEADED=1
dotnet test LMLocal.Tests.E2E\LMLocal.Tests.E2E.csproj
```

**CMD:**
```cmd
set HEADED=1
dotnet test LMLocal.Tests.E2E\LMLocal.Tests.E2E.csproj
```

Or add a `.runsettings` file to the project and select it in Visual Studio via
`Test → Configure Run Settings → Select Solution Wide runsettings File`:

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <EnvironmentVariables>
      <HEADED>1</HEADED>
    </EnvironmentVariables>
  </RunConfiguration>
</RunSettings>
```

---

## Structure

```
LMLocal.Tests.E2E\
??? TestAssets\
?   ??? test-app.html     # single test page — mirrors app.html (no mock scripts inside)
?   ??? webview-mock.js           # mock: connected, SUCCESS response
?   ??? webview-mock-offline.js   # mock: disconnected, ERROR response
?   ??? webview-mock-streaming.js # mock: fires ChatChunk + ChatComplete via postMessage
??? MainWindowTests.cs            # UI tests for app.html
??? README.md
```

## How it works

Since `app.html` runs inside WebView2 (not a regular browser),
tests use a single `test-app.html` — a copy of the page **without any mock scripts**.

Each test injects the required mock via `Page.AddInitScriptAsync()` before navigation,
so there is no HTML duplication per scenario.

## Keeping test-app.html in sync

`TestAssets/test-app.html` is **not stored in the repository**.
It is automatically copied from `LMLocal/Resources/app.html` on every build via `.csproj`
into the `bin/` output directory, which is already covered by `.gitignore`.
No manual sync needed — rebuilding the project is enough.

Mock scripts are injected by C# tests via `Page.AddInitScriptAsync()` — the HTML itself has no mock `<script>` tags.

## Adding new tests

Open `MainWindowTests.cs` and add a method with the `[TestMethod]` attribute:

```csharp
[TestMethod]
public async Task MyNewTest()
{
    await Expect(Page.Locator("#some-element")).ToBeVisibleAsync();
}
```

To test different bridge behavior, edit `TestAssets\webview-mock.js`.
