# SpriteGameOpenTk

A tiny, teaching-oriented sprite animation game built with OpenTK (OpenGL 3.3) and C#. It demonstrates window creation, shader compilation, VAO/VBO setup, orthographic projection, keyboard input, texture atlas sampling, and a simple frame-based animator that keeps the last frame visible when input stops.

## TL;DR

Install .NET, add NuGet packages (`OpenTK`, `OpenTK.Windowing.Desktop`, `SixLabors.ImageSharp`), place `Program.cs` and `Sprite_Character.png` in the project root, then run:

```bash
dotnet run
```

## Features

* Minimal, single-file `Program.cs` for clarity.
* OpenGL 3.3 pipeline: VAO/VBO, shaders, uniforms.
* Orthographic 2D rendering in pixel coordinates.
* Texture loading via ImageSharp (PNG → RGBA).
* Sprite-sheet sub-rect sampling (`uOffset`, `uSize`).
* Frame-based animation that **stops on last frame** when input stops.

## Requirements

* **.NET SDK**: 6.0 or newer.
* **OpenGL**: 3.3+ capable GPU/driver.
* OS: Windows, macOS, or Linux.

## Project Setup (CLI)

1. Create a console project:

   ```bash
   dotnet new console -n SpriteGameOpenTk
   cd SpriteGameOpenTk
   ```

2. Add dependencies:

   ```bash
   dotnet add package OpenTK --version 4.*
   dotnet add package OpenTK.Windowing.Desktop --version 4.*
   dotnet add package SixLabors.ImageSharp --version 3.*
   ```

3. Replace the generated `Program.cs` with the full code from this repo/snippet.

4. Add your sprite sheet image:

   * Save **`Sprite_Character.png`** in the project root (same folder as `.csproj`).
   * Ensure it’s copied to the output:

     ```xml
     <!-- SpriteGameOpenTk.csproj -->
     <Project Sdk="Microsoft.NET.Sdk">
       <PropertyGroup>
         <OutputType>Exe</OutputType>
         <TargetFramework>net8.0</TargetFramework>
       </PropertyGroup>
       <ItemGroup>
         <PackageReference Include="OpenTK" Version="4.*" />
         <PackageReference Include="OpenTK.Windowing.Desktop" Version="4.*" />
         <PackageReference Include="SixLabors.ImageSharp" Version="3.*" />
       </ItemGroup>
       <ItemGroup>
         <None Include="Sprite_Character.png" CopyToOutputDirectory="PreserveNewest" />
       </ItemGroup>
     </Project>
     ```

5. Run:

   ```bash
   dotnet run
   ```

## Running in IDEs

* **Visual Studio**: Open the folder/solution, restore packages, set project as startup, press **F5**.
* **VS Code**: Install “C#” extension, open folder, `dotnet build`, then `dotnet run` (or use Run & Debug).

## Controls

* **Right Arrow**: play right-walk row.
* **Left Arrow**: play left-walk row.
* **Release key**: animation **stays on the last shown frame**.

## Sprite Sheet Expectations

Adjust these `Character` constants to match your atlas:

* `FrameW`, `FrameH` — frame size (px).
* `Gap` — horizontal spacing between frames (px).
* `FrameCount` — columns per row.
* `SheetH` and implicit `SheetW` — sheet size (px).
  Default rows: `row 0 = Right`, `row 1 = Left`.

## How It Works (Short)

* **Window/Context**: `GameWindow` provides lifecycle (`OnLoad/OnUpdate/OnRender/OnUnload`).
* **Geometry**: A quad (4 verts) with interleaved position/UV in one VBO, bound via a VAO.
* **Shaders**:

  * Vertex: `projection * model` transform; flips V in UVs.
  * Fragment: samples atlas sub-rect via `uOffset + vTexCoord * uSize`.
* **Projection**: Orthographic pixels:

  ```csharp
  Matrix4.CreateOrthographicOffCenter(0, 800, 0, 600, -1, 1);
  ```
* **Texture**: ImageSharp decodes PNG → RGBA8; GL upload with **Nearest** filtering and **ClampToEdge** wrapping.
* **Animator**: Advances frames every `FrameTime` when moving; when direction is `None` it **does nothing**, leaving previous uniforms active so the last frame remains visible.

## Common Pitfalls & Fixes

* **Compile error**
  “Best overload for `CreateOrthographicOffCenter` does not have a parameter named `zNear`”
  ➜ Use **positional** args:

  ```csharp
  Matrix4 ortho = Matrix4.CreateOrthographicOffCenter(0, 800, 0, 600, -1, 1);
  ```
* **White rectangle / no texture**

  * Ensure `Sprite_Character.png` exists and is copied to output.
  * `uTexture` must be set to unit `0` and bound before drawing.
* **Texture bleeding**

  * Keep `Nearest` filter + `ClampToEdge`.
  * Add 1–2 px padding in the atlas or increase `Gap`.
* **Driver/Context issues**

  * Update GPU drivers; on Linux ensure OpenGL libs (Mesa) are installed.

## Suggested File Layout

```
SpriteGameOpenTk/
├─ SpriteGameOpenTk.csproj
├─ Program.cs
└─ Sprite_Character.png
```

## Extend This Sample

* Add WASD and actual movement.
* Flip geometry for left/right instead of separate rows.
* On-screen debug overlay (frame index, FPS).
* Multiple states (idle/walk/run) via a tiny state machine.

## License

MIT (or your preference).
