# GFxShaderMaker

Decompiled C# tool from Scaleform GFx SDK (`Bin/Tools/GFxShaderMaker/GFxShaderMaker.exe`).  
Generates shader source for multiple graphics platforms (D3D9, D3D11, GL, GLES, PS3, Orbis, Vita, Wii U, X360).

## Building

- **.NET Framework 3.5** (as decompiled): install [.NET Framework 3.5](https://dotnet.microsoft.com/download/dotnet-framework/net35) and build with MSBuild or Visual Studio.
- To use a newer SDK, change `TargetFramework` in `GFxShaderMaker.csproj` (e.g. `net48`) and fix any API differences.

```bash
dotnet build
# or
msbuild GFxShaderMaker.csproj
```

## Usage

Run the executable or use the decompiled project; the original tool is a console app with command-line options. Use `--help` (or equivalent) for supported actions and platforms.

## Origin

Source was produced by decompiling the original, non-obfuscated .NET assembly with [ILSpy](https://github.com/icsharpcode/ILSpy) / ilspycmd.  
This repository is for reference and improvement only; keep Scaleform/Adobe licensing in mind when reusing code.
