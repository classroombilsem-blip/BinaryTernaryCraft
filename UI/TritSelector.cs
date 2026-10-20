using System.Numerics;
using BinaryTernaryCraft.World;
using ImGuiNET;

namespace BinaryTernaryCraft.UI
{
    public class TritSelector
    {
        public bool IsActive { get; private set; }
        public int TargetX { get; private set; }
        public int TargetY { get; private set; }
        public int TargetZ { get; private set; }

        private Vector2 _startMousePos;
        public sbyte CurrentSelectedTrit { get; private set; } = 0;

        public void StartSelection(int x, int y, int z, Vector2 mousePos, sbyte currentTrit)
        {
            IsActive = true;
            TargetX = x;
            TargetY = y;
            TargetZ = z;
            _startMousePos = mousePos;
            CurrentSelectedTrit = currentTrit;
        }

        public void UpdateMouse(Vector2 currentMousePos)
        {
            if (!IsActive) return;

            float deltaX = currentMousePos.X - _startMousePos.X;
            const float threshold = 40.0f; // Pixels to switch stages

            if (deltaX < -threshold)
            {
                CurrentSelectedTrit = TritValue.Negative; // -1 (Red)
            }
            else if (deltaX > threshold)
            {
                CurrentSelectedTrit = TritValue.Positive; // +1 (Blue)
            }
            else
            {
                CurrentSelectedTrit = TritValue.Zero;     // 0 (Green)
            }
        }

        public sbyte FinishSelection()
        {
            IsActive = false;
            return CurrentSelectedTrit;
        }

        public void Render()
        {
            if (!IsActive) return;

            // Render 3-stage mini selector bar floating near mouse
            ImGui.SetNextWindowPos(new Vector2(_startMousePos.X - 90, _startMousePos.Y - 50));
            ImGui.SetNextWindowSize(new Vector2(180, 50));

            var flags = ImGuiWindowFlags.NoTitleBar |
                        ImGuiWindowFlags.NoResize |
                        ImGuiWindowFlags.NoMove |
                        ImGuiWindowFlags.NoScrollbar |
                        ImGuiWindowFlags.NoBackground;

            ImGui.Begin("##TritMiniSelector", flags);

            var drawList = ImGui.GetWindowDrawList();
            Vector2 p = ImGui.GetCursorScreenPos();
            float barW = 180.0f;
            float barH = 34.0f;

            // Background pill
            drawList.AddRectFilled(p, p + new Vector2(barW, barH), ImGui.ColorConvertFloat4ToU32(new Vector4(0.1f, 0.1f, 0.12f, 0.95f)), 6.0f);
            drawList.AddRect(p, p + new Vector2(barW, barH), ImGui.ColorConvertFloat4ToU32(new Vector4(0.4f, 0.4f, 0.45f, 1.0f)), 6.0f);

            // Three slices: Left (-1: Red), Middle (0: Green), Right (+1: Blue)
            float sliceW = barW / 3.0f;

            // Left Slice (-1 R)
            Vector2 r0 = p;
            Vector2 r1 = p + new Vector2(sliceW, barH);
            bool isR = CurrentSelectedTrit == -1;
            drawList.AddRectFilled(r0, r1, ImGui.ColorConvertFloat4ToU32(isR ? new Vector4(0.9f, 0.15f, 0.15f, 0.9f) : new Vector4(0.4f, 0.1f, 0.1f, 0.5f)), 4.0f);
            drawList.AddText(r0 + new Vector2(16, 8), ImGui.ColorConvertFloat4ToU32(Vector4.One), "-1 (R)");

            // Middle Slice (0 G)
            Vector2 g0 = p + new Vector2(sliceW, 0);
            Vector2 g1 = p + new Vector2(sliceW * 2, barH);
            bool isG = CurrentSelectedTrit == 0;
            drawList.AddRectFilled(g0, g1, ImGui.ColorConvertFloat4ToU32(isG ? new Vector4(0.15f, 0.85f, 0.25f, 0.9f) : new Vector4(0.1f, 0.4f, 0.15f, 0.5f)), 4.0f);
            drawList.AddText(g0 + new Vector2(18, 8), ImGui.ColorConvertFloat4ToU32(Vector4.One), "0 (G)");

            // Right Slice (+1 B)
            Vector2 b0 = p + new Vector2(sliceW * 2, 0);
            Vector2 b1 = p + new Vector2(barW, barH);
            bool isB = CurrentSelectedTrit == 1;
            drawList.AddRectFilled(b0, b1, ImGui.ColorConvertFloat4ToU32(isB ? new Vector4(0.15f, 0.45f, 0.95f, 0.9f) : new Vector4(0.1f, 0.2f, 0.5f, 0.5f)), 4.0f);
            drawList.AddText(b0 + new Vector2(16, 8), ImGui.ColorConvertFloat4ToU32(Vector4.One), "+1 (B)");

            ImGui.End();
        }
    }
}
