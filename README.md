# GFxShaderMaker

Decompiled C# tool from Scaleform GFx SDK. The **vanilla** branch contains the original decompiled code. (`Bin/Tools/GFxShaderMaker/GFxShaderMaker.exe`).  
Generates shader source for multiple graphics platforms (D3D9, D3D11, GL, GLES, PS3, Orbis, Vita, Wii U, X360).

## Building

The project targets **net35** (legacy, Windows x86) and **net8.0** (cross-platform). For local development and releases, use **.NET 8 SDK**.

**Quick build (net8.0):**
```bash
dotnet build -f net8.0
```

**Build scripts:**
- **Windows:** `.\build.ps1` — build; `.\build.ps1 -Publish` — single-file exe in `bin\Release\net8.0\win-x64\publish\`
- **Linux/macOS:** `./build.sh` — build; `./build.sh --publish` — single-file binary in `bin/Release/net8.0/<rid>/publish/`
- **Linux from Windows (WSL):** `.\build-linux-wsl.ps1` — builds Linux x64 binary via WSL (needs .NET 8 in WSL; script uses `/tmp/dotnet8` or install to `~/.dotnet` and add to PATH).

**Legacy .NET Framework 3.5** (Windows only): install [.NET Framework 3.5](https://dotnet.microsoft.com/download/dotnet-framework/net35) and run `dotnet build -f net35` or use MSBuild/Visual Studio.

## Releases

Releases are built from **version tags** via GitHub Actions. Pushing a tag (e.g. `v1.0.0`) triggers the workflow and produces:

- **Windows:** `GFxShaderMaker-Windows-x64.zip` — single-file `GFxShaderMaker.exe` (no .NET install required)
- **Linux:** `GFxShaderMaker-Linux-x64.zip` — single-file `GFxShaderMaker` executable

```bash
git tag v1.0.0
git push origin v1.0.0
```

Then open the repo’s **Releases** on GitHub to download the artifacts or create the release from the workflow run.

## Usage

Run the executable or use the decompiled project; the original tool is a console app with command-line options. Use `--help` (or equivalent) for supported actions and platforms.

## Origin

Source was produced by decompiling the original, non-obfuscated .NET assembly with [ILSpy](https://github.com/icsharpcode/ILSpy) / ilspycmd.  
This repository is for reference and improvement only; keep Scaleform/Adobe licensing in mind when reusing code.
