using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Numerics;
using BinaryTernaryCraft.Core;
using BinaryTernaryCraft.Kinetics;
using BinaryTernaryCraft.Logic;
using BinaryTernaryCraft.Models;
using BinaryTernaryCraft.Rendering;
using BinaryTernaryCraft.Shaders;
using BinaryTernaryCraft.UI;
using BinaryTernaryCraft.World;
using ImGuiNET;
using Silk.NET.Input;
using Silk.NET.Maths;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;
using GlShader = BinaryTernaryCraft.Shaders.Shader;

namespace BinaryTernaryCraft
{
    public static class Program
    {
        private static IWindow _window = null!;
        private static GL _gl = null!;
        private static IInputContext _input = null!;
        private static ImGuiController _imGuiController = null!;

        private static Camera _camera = null!;
        private static WorldGrid _world = null!;
        private static SimulationEngine _simEngine = null!;
        private static TextureAtlas _textureAtlas = null!;
        private static SSBOManager _ssboManager = null!;
        private static ChunkMesher _chunkMesher = null!;
        private static InstancedKineticRenderer _kineticRenderer = null!;

        private static GlShader _voxelShader = null!;
        private static GlShader _kineticShader = null!;

        private static readonly Dictionary<Chunk, ChunkMesh> _chunkMeshes = new();
        private static readonly Dictionary<BlockType, ParsedModel> _loadedModels = new();

        private static readonly TritSelector _tritSelector = new();
        private static readonly LogicAnalyzer _logicAnalyzer = new();
        private static bool _showDebugUI = true;
        private static BlockType _selectedPlacementType = BlockType.RGBWire;
        private static ushort _configuredTimer = 20;

        private static RaycastHit _currentHit;
        private static Vector2 _lastMousePos;
        private static bool _firstMouseMove = true;
        private static bool _cursorLocked = true;
        private static float _totalTime = 0.0f;

        private static readonly int[] _initialTritBuffer = new int[EngineConfig.TotalWorldVoxels];
        private static readonly float[] _initialKineticBuffer = new float[4096];

        public static void Main(string[] args)
        {
            var options = WindowOptions.Default;
            options.Size = new Vector2D<int>(EngineConfig.WindowWidth, EngineConfig.WindowHeight);
            options.Title = EngineConfig.WindowTitle;
            options.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(4, 5));
            options.VSync = EngineConfig.VSync;

            _window = Window.Create(options);
            _window.Load += OnLoad;
            _window.Update += OnUpdate;
            _window.Render += OnRender;
            _window.Closing += OnClosing;

            _window.Run();
        }

        private static void OnLoad()
        {
            _gl = _window.CreateOpenGL();
            _input = _window.CreateInput();
            _imGuiController = new ImGuiController(_gl, _window, _input);

            _gl.Enable(EnableCap.DepthTest);
            _gl.DepthFunc(DepthFunction.Less);
            _gl.Enable(EnableCap.CullFace);
            _gl.CullFace(TriangleFace.Back);

            _gl.ClearColor(0.53f, 0.72f, 0.95f, 1.0f); // Sky blue

            _camera = new Camera(new Vector3(16.0f, 12.0f, 16.0f), (float)_window.Size.X / _window.Size.Y);

            // 1. Load Bedrock .geo.json models
            LoadGeoModels();

            // 2. Initialize Texture Atlas & SSBO
            _textureAtlas = new TextureAtlas(_gl);
            _world = new WorldGrid(EngineConfig.WorldChunksX, EngineConfig.WorldChunksY, EngineConfig.WorldChunksZ);
            _ssboManager = new SSBOManager(_gl, EngineConfig.TotalWorldVoxels, 4096);
            _simEngine = new SimulationEngine(_world);

            // Populate initial test demo structures
            SetupDemoCircuit();

            // Initialize SSBO with current world states
            SyncAllStatesToSSBO();

            // Start background simulation thread (1.000 - 10.000 TPS)
            _simEngine.Start();

            // 3. Initialize Meshers & Shaders
            _chunkMesher = new ChunkMesher(_loadedModels);
            _kineticRenderer = new InstancedKineticRenderer(_gl, _loadedModels);

            _voxelShader = new GlShader(_gl, ShaderSources.VoxelVertexShader, ShaderSources.VoxelFragmentShader);
            _kineticShader = new GlShader(_gl, ShaderSources.KineticVertexShader, ShaderSources.KineticFragmentShader);

            // Initial chunk mesh generation
            RebuildDirtyChunkMeshes();
            _simEngine.KineticMgr.RebuildNetworkAt(16, 2, 20);
            _simEngine.KineticMgr.RebuildNetworkAt(20, 2, 19);
            _kineticRenderer.CollectInstances(_world, _simEngine.KineticMgr);

            // Input bindings
            if (_input.Mice.Count > 0)
            {
                var mouse = _input.Mice[0];
                mouse.Cursor.CursorMode = CursorMode.Disabled;
                mouse.MouseMove += OnMouseMove;
                mouse.MouseDown += OnMouseDown;
                mouse.MouseUp += OnMouseUp;
            }

            if (_input.Keyboards.Count > 0)
            {
                var keyboard = _input.Keyboards[0];
                keyboard.KeyDown += OnKeyDown;
            }
        }

        private static void LoadGeoModels()
        {
            var shaftModel = GeoJsonModelLoader.LoadFromJson(DefaultModels.KineticShaftGeoJson, "kinetic_shaft.geo.json");
            _loadedModels[BlockType.KineticShaft] = shaftModel;

            var gearModel = GeoJsonModelLoader.LoadFromJson(DefaultModels.KineticGearGeoJson, "kinetic_gear.geo.json");
            _loadedModels[BlockType.KineticGear] = gearModel;

            var motorModel = GeoJsonModelLoader.LoadFromJson(DefaultModels.ElectricMotorGeoJson, "electric_motor.geo.json");
            _loadedModels[BlockType.ElectricMotor] = motorModel;

            var pistonModel = GeoJsonModelLoader.LoadFromJson(DefaultModels.TernaryPistonGeoJson, "ternary_piston.geo.json");
            _loadedModels[BlockType.TernaryPiston] = pistonModel;
        }

        private static void SetupDemoCircuit()
        {
            // Line 1: Ternary Logic Line
            int x = 16, y = 2, z = 16;
            _world.SetBlock(x, y, z, (ushort)BlockType.TritSource, TritValue.Negative);
            _world.SetBlock(x + 1, y, z, (ushort)BlockType.RGBWire, TritValue.Negative);
            _world.SetBlock(x + 2, y, z, (ushort)BlockType.RGBWire, TritValue.Negative);
            _world.SetBlock(x + 3, y, z, (ushort)BlockType.NotGate, TritValue.Positive, Direction.East);
            _world.SetBlock(x + 4, y, z, (ushort)BlockType.RGBWire, TritValue.Positive);
            _world.SetBlock(x + 5, y, z, (ushort)BlockType.RGBWire, TritValue.Positive);
            _world.SetBlock(x + 6, y, z, (ushort)BlockType.RGBPixel, TritValue.Positive);

            // Line 2: Electric Motor & Kinetic Shaft/Gear line
            // TritSource at (16, 2, 18) -> RGB Wire at (16, 2, 19) -> Motor at (16, 2, 20) facing North
            _world.SetBlock(16, 2, 18, (ushort)BlockType.TritSource, TritValue.Positive); // +1 Blue turns motor forward
            _world.SetBlock(16, 2, 19, (ushort)BlockType.RGBWire, TritValue.Positive);
            _world.SetBlock(16, 2, 20, (ushort)BlockType.ElectricMotor, TritValue.Positive, Direction.North, 0, 1);
            _world.SetBlock(16, 2, 21, (ushort)BlockType.KineticShaft, 0, Direction.North, 0, 1);
            _world.SetBlock(16, 2, 22, (ushort)BlockType.KineticGear, 0, Direction.North, 0, 1);
            _world.SetBlock(17, 2, 22, (ushort)BlockType.KineticGear, 0, Direction.North, 0, 1); // Meshing gear with reversed rotation

            // Line 3: Ternary Piston Line
            // TritSource at (20, 2, 18) -> Piston at (20, 2, 19) facing South (+Z) -> 2 Pushable blocks at (20, 2, 20) and (20, 2, 21)
            _world.SetBlock(20, 2, 18, (ushort)BlockType.TritSource, TritValue.Negative); // -1 Retracted initially
            _world.SetBlock(20, 2, 19, (ushort)BlockType.TernaryPiston, TritValue.Negative, Direction.South, 0, 0);
            _world.SetBlock(20, 2, 20, (ushort)BlockType.Stone);
            _world.SetBlock(20, 2, 21, (ushort)BlockType.Stone);
        }

        private static void SyncAllStatesToSSBO()
        {
            for (int cx = 0; cx < _world.ChunksX; cx++)
            {
                for (int cy = 0; cy < _world.ChunksY; cy++)
                {
                    for (int cz = 0; cz < _world.ChunksZ; cz++)
                    {
                        var chunk = _world.GetChunk(cx, cy, cz);
                        if (chunk == null) continue;

                        int chunkLinearIndex = (cy * _world.ChunksZ + cz) * _world.ChunksX + cx;
                        int baseOffset = chunkLinearIndex * Chunk.VoxelCount;

                        for (int i = 0; i < Chunk.VoxelCount; i++)
                        {
                            _initialTritBuffer[baseOffset + i] = chunk.TritState[i];
                        }
                    }
                }
            }
            _ssboManager.UpdateAllStates(_initialTritBuffer);
        }

        private static void RebuildDirtyChunkMeshes()
        {
            var tempVerts = new List<VoxelVertex>(4096);
            var tempIndices = new List<uint>(8192);

            for (int cx = 0; cx < _world.ChunksX; cx++)
            {
                for (int cy = 0; cy < _world.ChunksY; cy++)
                {
                    for (int cz = 0; cz < _world.ChunksZ; cz++)
                    {
                        var chunk = _world.GetChunk(cx, cy, cz);
                        if (chunk == null || !chunk.IsMeshDirty) continue;

                        if (!_chunkMeshes.TryGetValue(chunk, out var mesh))
                        {
                            mesh = new ChunkMesh(_gl);
                            _chunkMeshes[chunk] = mesh;
                        }

                        _chunkMesher.BuildMesh(chunk, _world, tempVerts, tempIndices);
                        mesh.UploadData(tempVerts, tempIndices);
                        chunk.IsMeshDirty = false;
                    }
                }
            }
        }

        private static void OnUpdate(double dt)
        {
            float deltaTime = (float)dt;
            _totalTime += deltaTime;

            _imGuiController.Update(deltaTime);

            HandleKeyboardInput(deltaTime);

            // Perform voxel raycast from camera
            _currentHit = Raycast.Cast(_world, _camera.Position, _camera.Forward, EngineConfig.MaxReachDistance);

            // Record sample for Logic Analyzer / Oscilloscope
            _logicAnalyzer.UpdateSample(_world, _currentHit.BlockX, _currentHit.BlockY, _currentHit.BlockZ, _currentHit.HasHit);

            // Update Trit selector mouse drag if active
            if (_tritSelector.IsActive && _input.Mice.Count > 0)
            {
                _tritSelector.UpdateMouse(_input.Mice[0].Position);
            }

            // Sync kinetic instances and chunk meshes if piston or kinetics pushed blocks
            if (_simEngine.HasPendingInstanceUpdate)
            {
                RebuildDirtyChunkMeshes();
                _kineticRenderer.CollectInstances(_world, _simEngine.KineticMgr);
                _simEngine.HasPendingInstanceUpdate = false;
            }
        }

        private static void HandleKeyboardInput(float dt)
        {
            if (_input.Keyboards.Count == 0 || !_cursorLocked) return;
            var kb = _input.Keyboards[0];

            float speed = kb.IsKeyPressed(Key.ShiftLeft) ? _camera.FastMoveSpeed : _camera.MoveSpeed;

            Vector3 move = Vector3.Zero;
            if (kb.IsKeyPressed(Key.W)) move += _camera.Forward;
            if (kb.IsKeyPressed(Key.S)) move -= _camera.Forward;
            if (kb.IsKeyPressed(Key.A)) move -= _camera.Right;
            if (kb.IsKeyPressed(Key.D)) move += _camera.Right;
            if (kb.IsKeyPressed(Key.Space)) move += Vector3.UnitY;
            if (kb.IsKeyPressed(Key.ControlLeft)) move -= Vector3.UnitY;

            if (move != Vector3.Zero)
            {
                _camera.Position += Vector3.Normalize(move) * speed * dt;
            }
        }

        private static void OnMouseMove(IMouse mouse, Vector2 pos)
        {
            if (_tritSelector.IsActive)
            {
                _tritSelector.UpdateMouse(pos);
                return;
            }

            if (!_cursorLocked) return;

            if (_firstMouseMove)
            {
                _lastMousePos = pos;
                _firstMouseMove = false;
                return;
            }

            float xOffset = pos.X - _lastMousePos.X;
            float yOffset = pos.Y - _lastMousePos.Y;
            _lastMousePos = pos;

            _camera.ProcessMouseMovement(xOffset, yOffset);
        }

        private static void OnMouseDown(IMouse mouse, MouseButton button)
        {
            if (ImGui.GetIO().WantCaptureMouse) return;

            if (button == MouseButton.Left)
            {
                // Break block
                if (_currentHit.HasHit)
                {
                    _world.SetBlock(_currentHit.BlockX, _currentHit.BlockY, _currentHit.BlockZ, (ushort)BlockType.Air);
                    uint gIdx = _world.GetGlobalLogicIndex(_currentHit.BlockX, _currentHit.BlockY, _currentHit.BlockZ);
                    _ssboManager.UpdateSingleState(gIdx, 0);

                    _simEngine.NotifyBlockPlacedOrBroken(_currentHit.BlockX, _currentHit.BlockY, _currentHit.BlockZ, (ushort)BlockType.Air, 0);

                    RebuildDirtyChunkMeshes();
                    _kineticRenderer.CollectInstances(_world, _simEngine.KineticMgr);
                }
            }
            else if (button == MouseButton.Right)
            {
                if (_currentHit.HasHit)
                {
                    // Check if right-clicking TritSource -> start 3-stage mini selector
                    if (_currentHit.BlockId == (ushort)BlockType.TritSource)
                    {
                        sbyte currentTrit = _world.GetTritState(_currentHit.BlockX, _currentHit.BlockY, _currentHit.BlockZ);
                        _tritSelector.StartSelection(_currentHit.BlockX, _currentHit.BlockY, _currentHit.BlockZ, mouse.Position, currentTrit);
                        return;
                    }

                    // Otherwise place selected block against hit face
                    int placeX = _currentHit.BlockX + _currentHit.NormalX;
                    int placeY = _currentHit.BlockY + _currentHit.NormalY;
                    int placeZ = _currentHit.BlockZ + _currentHit.NormalZ;

                    if (_world.IsInsideWorld(placeX, placeY, placeZ))
                    {
                        byte dir = Direction.North;
                        if (_currentHit.NormalX != 0) dir = _currentHit.NormalX > 0 ? Direction.East : Direction.West;
                        else if (_currentHit.NormalY != 0) dir = _currentHit.NormalY > 0 ? Direction.Up : Direction.Down;
                        else if (_currentHit.NormalZ != 0) dir = _currentHit.NormalZ > 0 ? Direction.South : Direction.North;

                        _world.SetBlock(placeX, placeY, placeZ, (ushort)_selectedPlacementType, 0, dir, _configuredTimer, 1);
                        _simEngine.NotifyBlockPlacedOrBroken(placeX, placeY, placeZ, (ushort)_selectedPlacementType, 0);

                        RebuildDirtyChunkMeshes();
                        _kineticRenderer.CollectInstances(_world, _simEngine.KineticMgr);
                    }
                }
            }
        }

        private static void OnMouseUp(IMouse mouse, MouseButton button)
        {
            if (button == MouseButton.Right && _tritSelector.IsActive)
            {
                sbyte chosenTrit = _tritSelector.FinishSelection();
                int x = _tritSelector.TargetX;
                int y = _tritSelector.TargetY;
                int z = _tritSelector.TargetZ;

                _world.SetTritState(x, y, z, chosenTrit);
                uint gIdx = _world.GetGlobalLogicIndex(x, y, z);
                _ssboManager.UpdateSingleState(gIdx, chosenTrit);

                _simEngine.NotifyBlockPlacedOrBroken(x, y, z, (ushort)BlockType.TritSource, chosenTrit);
            }
        }

        private static void OnKeyDown(IKeyboard keyboard, Key key, int scancode)
        {
            if (key == Key.Escape)
            {
                _cursorLocked = !_cursorLocked;
                if (_input.Mice.Count > 0)
                {
                    _input.Mice[0].Cursor.CursorMode = _cursorLocked ? CursorMode.Disabled : CursorMode.Normal;
                    _firstMouseMove = true;
                }
            }
            else if (key == Key.P)
            {
                EngineConfig.SimulationPaused = !EngineConfig.SimulationPaused;
            }
            else if (key == Key.N)
            {
                EngineConfig.StepSingleTick = true;
            }
            else if (key == Key.F3)
            {
                _showDebugUI = !_showDebugUI;
            }
        }

        private static void OnRender(double dt)
        {
            // Flush pending simulation TritState changes to GPU SSBO
            _simEngine.SyncToSSBO(_ssboManager);

            _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

            Matrix4x4 view = _camera.GetViewMatrix();
            Matrix4x4 proj = _camera.GetProjectionMatrix();

            // 1. Render Static & Logic Voxel Chunk Meshes
            _voxelShader.Use();
            _voxelShader.SetUniform("uView", view);
            _voxelShader.SetUniform("uProjection", proj);
            _voxelShader.SetUniform("uTextureAtlas", 0);

            _textureAtlas.Bind(TextureUnit.Texture0);
            _ssboManager.Bind();

            foreach (var mesh in _chunkMeshes.Values)
            {
                mesh.Render();
            }

            // 2. Render GPU Instanced Kinetic Components
            _kineticShader.Use();
            _kineticShader.SetUniform("uView", view);
            _kineticShader.SetUniform("uProjection", proj);
            _kineticShader.SetUniform("uTime", _totalTime);
            _kineticShader.SetUniform("uTextureAtlas", 0);

            _kineticRenderer.RenderAll();

            // 3. Render ImGui UI
            if (_showDebugUI)
            {
                RenderDebugUI(dt);
                _imGuiController.Render();
            }
        }

        private static void RenderDebugUI(double dt)
        {
            // Render 3-stage mini selector floating bar
            _tritSelector.Render();

            // ==========================================
            // 1. Simulation & Execution Control Panel
            // ==========================================
            ImGui.SetNextWindowPos(new Vector2(10, 10), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(430, 240), ImGuiCond.FirstUseEver);

            ImGui.Begin("Simulation & Execution Dock", ImGuiWindowFlags.NoCollapse);

            ImGui.Text($"FPS: {(1.0 / dt):F1} ({(dt * 1000.0):F1} ms) | Cam: ({_camera.Position.X:F1}, {_camera.Position.Y:F1}, {_camera.Position.Z:F1})");
            ImGui.TextColored(new Vector4(0.2f, 1.0f, 0.4f, 1.0f), $"Live Measured TPS: {_simEngine.MeasuredTPS:F0} / Target: {EngineConfig.TargetTPS:F0}");
            ImGui.Text($"Simulation Tick: {_simEngine.CurrentTick:N0} | DirtyQueue: {_simEngine.ActiveDirtyCount:N0} | Timers: {_simEngine.ScheduledEventCount:N0}");
            ImGui.Separator();

            // Quick TPS Preset Buttons
            ImGui.Text("TPS Presets:");
            if (ImGui.Button("1 TPS")) EngineConfig.TargetTPS = 1;
            ImGui.SameLine();
            if (ImGui.Button("10 TPS")) EngineConfig.TargetTPS = 10;
            ImGui.SameLine();
            if (ImGui.Button("100 TPS")) EngineConfig.TargetTPS = 100;
            ImGui.SameLine();
            if (ImGui.Button("1K TPS")) EngineConfig.TargetTPS = 1000;
            ImGui.SameLine();
            if (ImGui.Button("10K TPS")) EngineConfig.TargetTPS = 10000;

            float tps = (float)EngineConfig.TargetTPS;
            if (ImGui.SliderFloat("##TargetTPS", ref tps, 1.0f, 10000.0f, "TPS: %.0f", ImGuiSliderFlags.Logarithmic))
            {
                EngineConfig.TargetTPS = tps;
            }

            ImGui.Separator();

            // Execution Controls
            bool isPaused = EngineConfig.SimulationPaused;
            if (ImGui.Button(isPaused ? "Resume Simulation (P)" : "Pause Simulation (P)", new Vector2(200, 28)))
            {
                EngineConfig.SimulationPaused = !EngineConfig.SimulationPaused;
            }

            ImGui.SameLine();
            if (ImGui.Button("Step 1 Tick (N)", new Vector2(150, 28)))
            {
                EngineConfig.StepSingleTick = true;
            }

            ImGui.TextDisabled("Shortcuts: [ESC] Mouse Lock | [P] Pause | [N] Step | [F3] Toggle UI");
            ImGui.End();

            // ==========================================
            // 2. Logic Analyzer / Oscilloscope
            // ==========================================
            ImGui.SetNextWindowPos(new Vector2(10, 260), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(430, 210), ImGuiCond.FirstUseEver);

            ImGui.Begin("Logic Analyzer / Oscilloscope (128 Ticks)", ImGuiWindowFlags.NoCollapse);

            int trackX = _logicAnalyzer.IsPinned ? _logicAnalyzer.PinnedX : (_currentHit.HasHit ? _currentHit.BlockX : 0);
            int trackY = _logicAnalyzer.IsPinned ? _logicAnalyzer.PinnedY : (_currentHit.HasHit ? _currentHit.BlockY : 0);
            int trackZ = _logicAnalyzer.IsPinned ? _logicAnalyzer.PinnedZ : (_currentHit.HasHit ? _currentHit.BlockZ : 0);

            if (_logicAnalyzer.IsPinned)
            {
                ImGui.TextColored(new Vector4(0.3f, 0.85f, 1.0f, 1.0f), $"Pinned: ({trackX}, {trackY}, {trackZ})");
                ImGui.SameLine();
                if (ImGui.SmallButton("Unpin"))
                {
                    _logicAnalyzer.Unpin();
                }
            }
            else
            {
                if (_currentHit.HasHit)
                {
                    ImGui.Text($"Tracking Raycast: ({trackX}, {trackY}, {trackZ})");
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Pin Block"))
                    {
                        _logicAnalyzer.Pin(trackX, trackY, trackZ);
                    }
                }
                else
                {
                    ImGui.TextDisabled("Tracking Raycast: (No block targeted)");
                }
            }

            sbyte liveTrit = _world.GetTritState(trackX, trackY, trackZ);
            Vector4 tritCol = liveTrit switch
            {
                -1 => new Vector4(1, 0.2f, 0.2f, 1),
                1 => new Vector4(0.2f, 0.5f, 1, 1),
                _ => new Vector4(0.2f, 0.9f, 0.3f, 1)
            };
            ImGui.SameLine(300);
            ImGui.TextColored(tritCol, $"Trit: {liveTrit}");

            _logicAnalyzer.RenderWaveform(405.0f, 110.0f);

            ImGui.End();

            // ==========================================
            // 3. Dynamic Block Inspector & Live Editor
            // ==========================================
            ImGui.SetNextWindowPos(new Vector2(10, 480), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(430, 360), ImGuiCond.FirstUseEver);

            ImGui.Begin("Block Inspector & Live Editor", ImGuiWindowFlags.NoCollapse);

            int inspX = _logicAnalyzer.IsPinned ? _logicAnalyzer.PinnedX : (_currentHit.HasHit ? _currentHit.BlockX : -1);
            int inspY = _logicAnalyzer.IsPinned ? _logicAnalyzer.PinnedY : (_currentHit.HasHit ? _currentHit.BlockY : -1);
            int inspZ = _logicAnalyzer.IsPinned ? _logicAnalyzer.PinnedZ : (_currentHit.HasHit ? _currentHit.BlockZ : -1);

            if (inspX >= 0 && _world.IsInsideWorld(inspX, inspY, inspZ))
            {
                ushort id = _world.GetBlockId(inspX, inspY, inspZ);
                BlockType bType = (BlockType)id;
                sbyte bTrit = _world.GetTritState(inspX, inspY, inspZ);
                byte bMeta = _world.GetBlockMeta(inspX, inspY, inspZ);
                ushort bTimer = _world.GetTimerConfig(inspX, inspY, inspZ);
                int bNet = _world.GetNetworkId(inspX, inspY, inspZ);
                uint gIdx = _world.GetGlobalLogicIndex(inspX, inspY, inspZ);

                ImGui.Text($"Target Coordinates: ({inspX}, {inspY}, {inspZ})");
                ImGui.Text($"Block Type: {bType}");
                ImGui.Text($"GlobalLogicIndex: {gIdx} | Facing: {bMeta}");

                Vector4 bCol = bTrit switch
                {
                    -1 => new Vector4(1, 0.2f, 0.2f, 1),
                    1 => new Vector4(0.2f, 0.5f, 1, 1),
                    _ => new Vector4(0.2f, 0.9f, 0.3f, 1)
                };
                ImGui.TextColored(bCol, $"TritState: {bTrit} ({(bTrit == -1 ? "Red" : bTrit == 1 ? "Blue" : "Green")})");
                ImGui.Text($"Timer Config: {bTimer} ticks");
                ImGui.Text($"Network ID: {bNet}");

                if (BlockCategory.IsKinetic(bType))
                {
                    float ratio = _simEngine.KineticMgr.GetGearRatio(inspX, inspY, inspZ);
                    ImGui.TextColored(new Vector4(1.0f, 0.75f, 0.2f, 1.0f), $"Kinetic Gear Ratio: {ratio:F2}x");

                    if (bType == BlockType.ElectricMotor)
                    {
                        ImGui.TextColored(new Vector4(0.3f, 0.9f, 1.0f, 1.0f), $"Motor Speed: {bTrit * KineticNetworkManager.BaseMotorRPM:F1} rad/s");
                    }
                    else if (bType == BlockType.TernaryPiston)
                    {
                        ImGui.TextColored(new Vector4(0.3f, 0.9f, 1.0f, 1.0f), $"Piston Extension: {bTimer} / 2 blocks");
                    }
                }

                ImGui.Separator();
                ImGui.TextColored(new Vector4(0.9f, 0.8f, 0.2f, 1.0f), "Live Block Editor:");

                // If block is a TritSource, provide direct state toggle buttons
                if (bType == BlockType.TritSource)
                {
                    ImGui.Text("Set Source Trit Value:");
                    if (ImGui.Button("-1 (Red)", new Vector2(80, 24)))
                    {
                        _world.SetTritState(inspX, inspY, inspZ, -1);
                        _ssboManager.UpdateSingleState(gIdx, -1);
                        _simEngine.NotifyBlockPlacedOrBroken(inspX, inspY, inspZ, (ushort)BlockType.TritSource, -1);
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("0 (Green)", new Vector2(80, 24)))
                    {
                        _world.SetTritState(inspX, inspY, inspZ, 0);
                        _ssboManager.UpdateSingleState(gIdx, 0);
                        _simEngine.NotifyBlockPlacedOrBroken(inspX, inspY, inspZ, (ushort)BlockType.TritSource, 0);
                    }
                    ImGui.SameLine();
                    if (ImGui.Button("+1 (Blue)", new Vector2(80, 24)))
                    {
                        _world.SetTritState(inspX, inspY, inspZ, 1);
                        _ssboManager.UpdateSingleState(gIdx, 1);
                        _simEngine.NotifyBlockPlacedOrBroken(inspX, inspY, inspZ, (ushort)BlockType.TritSource, 1);
                    }
                }

                // Live Timer / Pulse Duration Adjustment
                int liveTimer = bTimer;
                if (ImGui.SliderInt("Live Timer Config (Ticks)", ref liveTimer, 1, 500))
                {
                    _world.SetTimerConfig(inspX, inspY, inspZ, (ushort)liveTimer);
                }
            }
            else
            {
                ImGui.TextDisabled("No block targeted or pinned.");
            }

            ImGui.End();

            // ==========================================
            // 4. Block Placement Palette / Hotbar
            // ==========================================
            ImGui.SetNextWindowPos(new Vector2(EngineConfig.WindowWidth - 360, 10), ImGuiCond.FirstUseEver);
            ImGui.SetNextWindowSize(new Vector2(340, 520), ImGuiCond.FirstUseEver);

            ImGui.Begin("Placement Palette & Hotbar", ImGuiWindowFlags.NoCollapse);

            ImGui.TextColored(new Vector4(0.4f, 0.8f, 1.0f, 1.0f), "Select Block to Place (Right Click):");
            var blockNames = Enum.GetNames<BlockType>();
            for (int i = 1; i < blockNames.Length; i++)
            {
                BlockType bt = (BlockType)i;
                bool isSelected = _selectedPlacementType == bt;
                if (ImGui.RadioButton(blockNames[i], isSelected))
                {
                    _selectedPlacementType = bt;
                }
                if (i % 2 != 0) ImGui.SameLine(170);
            }

            ImGui.NewLine();
            ImGui.Separator();

            // Placement default timer config
            int timerVal = _configuredTimer;
            if (ImGui.SliderInt("Default Timer Setting", ref timerVal, 1, 500))
            {
                _configuredTimer = (ushort)timerVal;
            }

            ImGui.End();
        }

        private static void OnClosing()
        {
            _simEngine.Dispose();
            _imGuiController.Dispose();
            _voxelShader.Dispose();
            _kineticShader.Dispose();
            _textureAtlas.Dispose();
            _ssboManager.Dispose();
            _kineticRenderer.Dispose();

            foreach (var mesh in _chunkMeshes.Values)
            {
                mesh.Dispose();
            }
            _chunkMeshes.Clear();

            _gl.Dispose();
        }
    }
}
