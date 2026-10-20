using System;
using System.Numerics;
using BinaryTernaryCraft.World;
using ImGuiNET;

namespace BinaryTernaryCraft.UI
{
    public class LogicAnalyzer
    {
        public const int SampleHistoryCount = 128;

        private readonly sbyte[] _history = new sbyte[SampleHistoryCount];
        private int _head = 0;

        public bool IsPinned { get; set; } = false;
        public int PinnedX { get; set; }
        public int PinnedY { get; set; }
        public int PinnedZ { get; set; }

        public void Pin(int x, int y, int z)
        {
            IsPinned = true;
            PinnedX = x;
            PinnedY = y;
            PinnedZ = z;
        }

        public void Unpin()
        {
            IsPinned = false;
        }

        public void RecordSample(sbyte trit)
        {
            _history[_head] = trit;
            _head = (_head + 1) % SampleHistoryCount;
        }

        public void UpdateSample(WorldGrid world, int raycastX, int raycastY, int raycastZ, bool hasRaycastHit)
        {
            int targetX = IsPinned ? PinnedX : raycastX;
            int targetY = IsPinned ? PinnedY : raycastY;
            int targetZ = IsPinned ? PinnedZ : raycastZ;

            if (IsPinned || hasRaycastHit)
            {
                if (world.IsInsideWorld(targetX, targetY, targetZ))
                {
                    sbyte currentTrit = world.GetTritState(targetX, targetY, targetZ);
                    RecordSample(currentTrit);
                    return;
                }
            }

            RecordSample(0);
        }

        public void RenderWaveform(float width = 360.0f, float height = 90.0f)
        {
            var drawList = ImGui.GetWindowDrawList();
            Vector2 origin = ImGui.GetCursorScreenPos();
            Vector2 rectEnd = origin + new Vector2(width, height);

            // Oscilloscope screen background (Dark phosphor scope look)
            uint bgCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.04f, 0.06f, 0.08f, 0.95f));
            uint borderCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.2f, 0.35f, 0.45f, 1.0f));
            drawList.AddRectFilled(origin, rectEnd, bgCol, 4.0f);
            drawList.AddRect(origin, rectEnd, borderCol, 4.0f);

            // Three reference level lines:
            // Y levels: +1 (top), 0 (center), -1 (bottom)
            float padY = 16.0f;
            float topY = origin.Y + padY;
            float midY = origin.Y + height * 0.5f;
            float botY = origin.Y + height - padY;

            uint gridLineCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.18f, 0.22f, 0.26f, 0.7f));
            drawList.AddLine(new Vector2(origin.X, topY), new Vector2(rectEnd.X, topY), gridLineCol, 1.0f);
            drawList.AddLine(new Vector2(origin.X, midY), new Vector2(rectEnd.X, midY), gridLineCol, 1.0f);
            drawList.AddLine(new Vector2(origin.X, botY), new Vector2(rectEnd.X, botY), gridLineCol, 1.0f);

            // Level text labels
            uint labelCol = ImGui.ColorConvertFloat4ToU32(new Vector4(0.6f, 0.7f, 0.8f, 0.6f));
            drawList.AddText(new Vector2(origin.X + 6, topY - 14), labelCol, "+1 (Blue)");
            drawList.AddText(new Vector2(origin.X + 6, midY - 7), labelCol, " 0 (Green)");
            drawList.AddText(new Vector2(origin.X + 6, botY - 2), labelCol, "-1 (Red)");

            // Draw Digital Square Waveform
            float stepX = width / (SampleHistoryCount - 1);
            float prevX = origin.X;
            sbyte prevTrit = _history[_head]; // Oldest sample
            float prevY = GetYForTrit(prevTrit, topY, midY, botY);

            for (int i = 1; i < SampleHistoryCount; i++)
            {
                int sampleIdx = (_head + i) % SampleHistoryCount;
                sbyte currTrit = _history[sampleIdx];

                float currX = origin.X + i * stepX;
                float currY = GetYForTrit(currTrit, topY, midY, botY);

                uint waveColor = currTrit switch
                {
                    -1 => ImGui.ColorConvertFloat4ToU32(new Vector4(1.0f, 0.2f, 0.2f, 1.0f)),
                    1 => ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.55f, 1.0f, 1.0f)),
                    _ => ImGui.ColorConvertFloat4ToU32(new Vector4(0.15f, 0.85f, 0.3f, 1.0f))
                };

                // Vertical transition step if value changed
                if (currY != prevY)
                {
                    drawList.AddLine(new Vector2(currX, prevY), new Vector2(currX, currY), waveColor, 2.0f);
                }

                // Horizontal hold line
                drawList.AddLine(new Vector2(prevX, prevY), new Vector2(currX, prevY), waveColor, 2.0f);

                prevX = currX;
                prevY = currY;
            }

            // Reserve dummy space in ImGui layout
            ImGui.Dummy(new Vector2(width, height));
        }

        private static float GetYForTrit(sbyte trit, float topY, float midY, float botY)
        {
            return trit switch
            {
                1 => topY,
                -1 => botY,
                _ => midY
            };
        }
    }
}
