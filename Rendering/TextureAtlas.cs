using System;
using System.IO;
using System.Numerics;
using BinaryTernaryCraft.World;
using Silk.NET.OpenGL;
using StbImageSharp;

namespace BinaryTernaryCraft.Rendering
{
    public class TextureAtlas : IDisposable
    {
        private readonly GL _gl;
        public uint TextureHandle { get; private set; }

        public const int TileResolution = 16;
        public const int AtlasTilesX = 16;
        public const int AtlasTilesY = 16;
        public const int AtlasWidth = TileResolution * AtlasTilesX;  // 256
        public const int AtlasHeight = TileResolution * AtlasTilesY; // 256

        public TextureAtlas(GL gl, string? customPngPath = null)
        {
            _gl = gl;
            LoadOrCreateAtlas(customPngPath);
        }

        private unsafe void LoadOrCreateAtlas(string? customPngPath)
        {
            byte[] pixelData;
            int width = AtlasWidth;
            int height = AtlasHeight;

            if (!string.IsNullOrEmpty(customPngPath) && File.Exists(customPngPath))
            {
                using var stream = File.OpenRead(customPngPath);
                var image = ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
                width = image.Width;
                height = image.Height;
                pixelData = image.Data;
            }
            else
            {
                pixelData = GenerateProceduralAtlas();
            }

            TextureHandle = _gl.GenTexture();
            _gl.BindTexture(TextureTarget.Texture2D, TextureHandle);

            fixed (byte* ptr = pixelData)
            {
                _gl.TexImage2D(
                    TextureTarget.Texture2D,
                    0,
                    InternalFormat.Rgba8,
                    (uint)width,
                    (uint)height,
                    0,
                    PixelFormat.Rgba,
                    PixelType.UnsignedByte,
                    ptr);
            }

            // Crisp nearest-neighbor pixel filtering
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
            _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);

            _gl.BindTexture(TextureTarget.Texture2D, 0);
        }

        public static (Vector2 uvMin, Vector2 uvMax) GetUVs(BlockType type, byte direction)
        {
            // Map BlockType to tile (tileX, tileY) in 16x16 grid
            int tileX = 0;
            int tileY = 0;

            switch (type)
            {
                case BlockType.Stone:
                    tileX = 1; tileY = 0;
                    break;
                case BlockType.RGBWire:
                    tileX = 2; tileY = 0;
                    break;
                case BlockType.TritSource:
                    tileX = 3; tileY = 0;
                    break;
                case BlockType.NotGate:
                    tileX = 4; tileY = 0;
                    break;
                case BlockType.CycleGate:
                    tileX = 5; tileY = 0;
                    break;
                case BlockType.AntiCycleGate:
                    tileX = 6; tileY = 0;
                    break;
                case BlockType.MinGate:
                    tileX = 7; tileY = 0;
                    break;
                case BlockType.MaxGate:
                    tileX = 8; tileY = 0;
                    break;
                case BlockType.SumGate:
                    tileX = 9; tileY = 0;
                    break;
                case BlockType.ConsensusGate:
                    tileX = 10; tileY = 0;
                    break;
                case BlockType.PulseGenerator:
                    tileX = 11; tileY = 0;
                    break;
                case BlockType.PulseExtender:
                    tileX = 12; tileY = 0;
                    break;
                case BlockType.ToggleLatch:
                    tileX = 13; tileY = 0;
                    break;
                case BlockType.TritLatch:
                    tileX = 14; tileY = 0;
                    break;
                case BlockType.RGBPixel:
                    tileX = 15; tileY = 0;
                    break;
                case BlockType.KineticShaft:
                    tileX = 0; tileY = 1;
                    break;
                case BlockType.KineticGear:
                    tileX = 1; tileY = 1;
                    break;
                case BlockType.ElectricMotor:
                    tileX = 2; tileY = 1;
                    break;
                case BlockType.TernaryPiston:
                    tileX = 3; tileY = 1;
                    break;
                default:
                    tileX = 0; tileY = 0;
                    break;
            }

            float u0 = (float)tileX / AtlasTilesX;
            float v0 = (float)tileY / AtlasTilesY;
            float u1 = (float)(tileX + 1) / AtlasTilesX;
            float v1 = (float)(tileY + 1) / AtlasTilesY;

            return (new Vector2(u0, v0), new Vector2(u1, v1));
        }

        private static byte[] GenerateProceduralAtlas()
        {
            byte[] data = new byte[AtlasWidth * AtlasHeight * 4];

            for (int ty = 0; ty < AtlasTilesY; ty++)
            {
                for (int tx = 0; tx < AtlasTilesX; tx++)
                {
                    DrawTile(data, tx, ty);
                }
            }

            return data;
        }

        private static void DrawTile(byte[] data, int tileX, int tileY)
        {
            int startX = tileX * TileResolution;
            int startY = tileY * TileResolution;

            for (int py = 0; py < TileResolution; py++)
            {
                for (int px = 0; px < TileResolution; px++)
                {
                    int x = startX + px;
                    int y = startY + py;
                    int index = (y * AtlasWidth + x) * 4;

                    byte r = 180, g = 180, b = 180, a = 255;
                    bool border = px == 0 || px == TileResolution - 1 || py == 0 || py == TileResolution - 1;

                    if (tileY == 0)
                    {
                        switch (tileX)
                        {
                            case 0: // Air / Blank
                                r = 255; g = 255; b = 255; a = 0;
                                break;
                            case 1: // Stone
                                byte noise = (byte)(((px * 37) ^ (py * 59)) % 30);
                                r = (byte)(120 + noise);
                                g = (byte)(120 + noise);
                                b = (byte)(125 + noise);
                                if (border) { r -= 30; g -= 30; b -= 30; }
                                break;
                            case 2: // RGB Wire
                                r = 50; g = 50; b = 55;
                                if ((px >= 5 && px <= 10) || (py >= 5 && py <= 10))
                                {
                                    // Logic wire channel core
                                    r = 200; g = 200; b = 200;
                                }
                                break;
                            case 3: // Trit Source
                                r = 70; g = 70; b = 80;
                                if (px >= 4 && px <= 11 && py >= 4 && py <= 11)
                                {
                                    r = 230; g = 230; b = 240; // Emissive core
                                }
                                break;
                            case 4: // NotGate
                            case 5: // CycleGate
                            case 6: // AntiCycleGate
                            case 7: // MinGate
                            case 8: // MaxGate
                            case 9: // SumGate
                            case 10: // ConsensusGate
                                r = 60; g = 65; b = 75;
                                if (border) { r = 40; g = 40; b = 50; }
                                if (px >= 4 && px <= 11 && py >= 4 && py <= 11)
                                {
                                    r = 210; g = 210; b = 220; // Symbol center
                                }
                                break;
                            case 15: // RGB Pixel
                                r = 220; g = 220; b = 220;
                                break;
                            default:
                                r = (byte)(100 + tileX * 8);
                                g = (byte)(100 + tileY * 8);
                                b = 120;
                                break;
                        }
                    }
                    else if (tileY == 1) // Kinetics
                    {
                        switch (tileX)
                        {
                            case 0: // Shaft
                                r = 140; g = 100; b = 60; // Wood/copper
                                if (border) { r -= 20; g -= 20; b -= 10; }
                                break;
                            case 1: // Gear
                                r = 160; g = 140; b = 70; // Brass
                                break;
                            case 2: // Motor
                                r = 45; g = 75; b = 110; // Blue steel
                                break;
                            case 3: // Piston
                                r = 130; g = 130; b = 135; // Iron
                                break;
                            default:
                                r = 128; g = 128; b = 128;
                                break;
                        }
                    }

                    data[index + 0] = r;
                    data[index + 1] = g;
                    data[index + 2] = b;
                    data[index + 3] = a;
                }
            }
        }

        public void Bind(TextureUnit unit = TextureUnit.Texture0)
        {
            _gl.ActiveTexture(unit);
            _gl.BindTexture(TextureTarget.Texture2D, TextureHandle);
        }

        public void Dispose()
        {
            if (TextureHandle != 0)
            {
                _gl.DeleteTexture(TextureHandle);
                TextureHandle = 0;
            }
        }
    }
}
