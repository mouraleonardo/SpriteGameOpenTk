// File: Program.cs
//
// Fix explained: some OpenTK versions expose
// Matrix4.CreateOrthographicOffCenter(float left, float right, float bottom, float top, float zNear, float zFar)
// but older/newer API names may differ (e.g., near, far). Using *named* args can break across versions.
// We switch to *positional* args to be version-agnostic: CreateOrthographicOffCenter(0, 800, 0, 600, -1, 1).

using OpenTK.Graphics.OpenGL4;                       // OpenGL API
using OpenTK.Windowing.Common;                       // Frame events (OnLoad/OnUpdate/OnRender)
using OpenTK.Windowing.Desktop;                      // GameWindow/NativeWindowSettings
using OpenTK.Windowing.GraphicsLibraryFramework;     // Keyboard state
using OpenTK.Mathematics;                            // Matrix4, Vector types
using System;
using System.IO;
using ImageSharp = SixLabors.ImageSharp.Image;       // Alias for brevity
using SixLabors.ImageSharp.PixelFormats;             // Rgba32 pixel type

namespace OpenTK_Sprite_Animation
{
    public class SpriteAnimationGame : GameWindow
    {
        private Character _character;                 // Handles animation state + UV selection
        private int _shaderProgram;                   // Linked GLSL program
        private int _vao, _vbo;                       // Geometry
        private int _texture;                         // Sprite sheet

        public SpriteAnimationGame()
            : base(
                new GameWindowSettings(),
                new NativeWindowSettings { Size = (800, 600), Title = "Sprite Animation" })
        { }

        protected override void OnLoad()
        {
            base.OnLoad();

            GL.ClearColor(0f, 0f, 0f, 0f);            // Transparent background (A=0)
            GL.Enable(EnableCap.Blend);               // Enable alpha blending
            GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);

            _shaderProgram = CreateShaderProgram();   // Compile + link
            _texture = LoadTexture("Sprite_Character.png"); // Upload sprite sheet

            // Quad vertices: [pos.x, pos.y, uv.x, uv.y], centered model space
            float w = 32f, h = 64f;                   // Half-size: results in 64x128 sprite
            float[] vertices =
            {
                -w, -h, 0f, 0f,
                 w, -h, 1f, 0f,
                 w,  h, 1f, 1f,
                -w,  h, 0f, 1f
            };

            _vao = GL.GenVertexArray();
            GL.BindVertexArray(_vao);

            _vbo = GL.GenBuffer();
            GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, vertices.Length * sizeof(float), vertices, BufferUsageHint.StaticDraw);

            // Attribute 0: vec2 position
            GL.EnableVertexAttribArray(0);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 0);

            // Attribute 1: vec2 texcoord
            GL.EnableVertexAttribArray(1);
            GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, 4 * sizeof(float), 2 * sizeof(float));

            GL.UseProgram(_shaderProgram);

            // Bind sampler to texture unit 0 (WHY: avoid undefined default binding)
            int texLoc = GL.GetUniformLocation(_shaderProgram, "uTexture");
            GL.Uniform1(texLoc, 0);

            // Orthographic projection (pixel coordinates 0..800, 0..600)
            // IMPORTANT: positional args to avoid API-name mismatch across OpenTK versions.
            int projLoc = GL.GetUniformLocation(_shaderProgram, "projection");
            Matrix4 ortho = Matrix4.CreateOrthographicOffCenter(0, 800, 0, 600, -1, 1);
            GL.UniformMatrix4(projLoc, false, ref ortho);

            // Model transform: place the quad at window center (400,300)
            int modelLoc = GL.GetUniformLocation(_shaderProgram, "model");
            Matrix4 model = Matrix4.CreateTranslation(400, 300, 0);
            GL.UniformMatrix4(modelLoc, false, ref model);

            _character = new Character(_shaderProgram); // Initializes idle frame uniforms
        }

        protected override void OnUpdateFrame(FrameEventArgs e)
        {
            base.OnUpdateFrame(e);

            // Read keyboard state -> map to Direction
            var keyboard = KeyboardState;
            Direction dir = Direction.None;
            if (keyboard.IsKeyDown(Keys.Right)) dir = Direction.Right;
            else if (keyboard.IsKeyDown(Keys.Left)) dir = Direction.Left;

            // Animation update; when dir == None we *keep last frame visible*
            _character.Update((float)e.Time, dir);
        }

        protected override void OnRenderFrame(FrameEventArgs e)
        {
            base.OnRenderFrame(e);

            GL.Clear(ClearBufferMask.ColorBufferBit);

            // Bind texture and VAO, then draw
            GL.ActiveTexture(TextureUnit.Texture0);
            GL.BindTexture(TextureTarget.Texture2D, _texture);
            GL.BindVertexArray(_vao);

            _character.Render();

            SwapBuffers();
        }

        protected override void OnUnload()
        {
            // Free GPU resources
            GL.DeleteProgram(_shaderProgram);
            GL.DeleteTexture(_texture);
            GL.DeleteBuffer(_vbo);
            GL.DeleteVertexArray(_vao);
            base.OnUnload();
        }

        // --- Shader creation utilities ---------------------------------------------------------

        private int CreateShaderProgram()
        {
            // Vertex Shader: transforms positions, flips V in UVs (image origin vs GL origin)
            string vs = @"
#version 330 core
layout(location = 0) in vec2 aPosition;
layout(location = 1) in vec2 aTexCoord;
out vec2 vTexCoord;
uniform mat4 projection;
uniform mat4 model;
void main() {
    gl_Position = projection * model * vec4(aPosition, 0.0, 1.0);
    vTexCoord = vec2(aTexCoord.x, 1.0 - aTexCoord.y); // flip V so PNGs read intuitively
}";

            // Fragment Shader: samples sub-rect of the sheet using uOffset/uSize
            string fs = @"
#version 330 core
in vec2 vTexCoord;
out vec4 color;
uniform sampler2D uTexture; // bound to texture unit 0
uniform vec2 uOffset;       // normalized UV start (0..1)
uniform vec2 uSize;         // normalized UV size  (0..1)
void main() {
    vec2 uv = uOffset + vTexCoord * uSize;
    color = texture(uTexture, uv);
}";

            int v = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(v, vs);
            GL.CompileShader(v);
            CheckShaderCompile(v, "VERTEX");

            int f = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(f, fs);
            GL.CompileShader(f);
            CheckShaderCompile(f, "FRAGMENT");

            int p = GL.CreateProgram();
            GL.AttachShader(p, v);
            GL.AttachShader(p, f);
            GL.LinkProgram(p);
            CheckProgramLink(p);

            GL.DetachShader(p, v);
            GL.DetachShader(p, f);
            GL.DeleteShader(v);
            GL.DeleteShader(f);

            return p;
        }

        private static void CheckShaderCompile(int shader, string stage)
        {
            GL.GetShader(shader, ShaderParameter.CompileStatus, out int ok);
            if (ok == 0)
                throw new Exception($"{stage} SHADER COMPILE ERROR:\n{GL.GetShaderInfoLog(shader)}");
        }

        private static void CheckProgramLink(int program)
        {
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int ok);
            if (ok == 0)
                throw new Exception($"PROGRAM LINK ERROR:\n{GL.GetProgramInfoLog(program)}");
        }

        // --- Texture loading ------------------------------------------------------------------

        private int LoadTexture(string path)
        {
            if (!File.Exists(path))
                throw new FileNotFoundException($"Texture not found: {path}", path);

            using var img = ImageSharp.Load<Rgba32>(path); // decode to RGBA8

            int tex = GL.GenTexture();
            GL.BindTexture(TextureTarget.Texture2D, tex);

            // Copy raw pixels to managed buffer then upload
            var pixels = new byte[4 * img.Width * img.Height];
            img.CopyPixelDataTo(pixels);

            GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba,
                          img.Width, img.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, pixels);

            GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);

            // Nearest: prevents bleeding between adjacent frames on the atlas
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            // Clamp: avoid wrap artifacts at frame borders
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);

            return tex;
        }
    }

    // --- Direction input abstraction -----------------------------------------------------------
    public enum Direction { None, Right, Left }

    // --- Animator ------------------------------------------------------------------------------
    public class Character
    {
        private readonly int _shader;  // Program containing uOffset/uSize
        private float _timer;          // Accumulated time for frame stepping
        private int _frame;            // Current frame column (0..FrameCount-1)
        private Direction _currentDir; // Last non-none direction

        // Timing
        private const float FrameTime = 0.15f; // seconds per frame
        private const int FrameCount = 4;      // frames per row

        // Sprite sheet layout (pixel units) — edit to match your atlas
        private const float FrameW = 64f;
        private const float FrameH = 128f;
        private const float Gap = 60f;      // horizontal spacing between frames
        private const float TotalW = FrameW + Gap;
        private const float SheetW = 4 * TotalW - Gap; // 4 columns
        private const float SheetH = 256f;     // 2 rows of 128 => 256

        public Character(int shader)
        {
            _shader = shader;
            _currentDir = Direction.None;
            SetIdle();                          // Pick a visible starting frame
        }

        public void Update(float delta, Direction dir)
        {
            // Requirement: when input stops, keep showing the last used frame.
            if (dir == Direction.None)
            {
                _currentDir = Direction.None;   // Remember idle, but DON'T touch uniforms
                return;
            }

            // If started moving or changed side: restart cycle
            if (_currentDir != dir)
            {
                _timer = 0f;
                _frame = 0;
                _currentDir = dir;
            }

            _timer += delta;
            if (_timer >= FrameTime)
            {
                _timer -= FrameTime;
                _frame = (_frame + 1) % FrameCount;
            }

            int row = dir == Direction.Right ? 0 : 1; // Row per direction
            SetFrame(_frame, row);                    // Update UV uniforms
        }

        public void Render()
        {
            GL.DrawArrays(PrimitiveType.TriangleFan, 0, 4); // Draw quad
        }

        private void SetIdle()
        {
            SetFrame(0, 0); // Any default you prefer
        }

        // Converts (col,row) in pixels to normalized UVs and uploads to shader
        private void SetFrame(int col, int row)
        {
            float x = (col * TotalW) / SheetW; // normalized start U
            float y = (row * FrameH) / SheetH; // normalized start V
            float w = FrameW / SheetW;         // normalized width
            float h = FrameH / SheetH;         // normalized height

            GL.UseProgram(_shader);
            int off = GL.GetUniformLocation(_shader, "uOffset");
            int sz = GL.GetUniformLocation(_shader, "uSize");
            GL.Uniform2(off, x, y);
            GL.Uniform2(sz, w, h);
        }
    }

    // --- Entry point ---------------------------------------------------------------------------
    internal class Program
    {
        private static void Main()
        {
            using var game = new SpriteAnimationGame(); // Ensures Dispose/OnUnload is called
            game.Run();                                  // Game loop: Load -> (Update/Render)* -> Unload
        }
    }
}
