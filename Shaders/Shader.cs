using System;
using System.IO;
using System.Numerics;
using Silk.NET.OpenGL;

namespace BinaryTernaryCraft.Shaders
{
    public class Shader : IDisposable
    {
        private readonly GL _gl;
        public readonly uint Handle;

        public Shader(GL gl, string vertexSource, string fragmentSource)
        {
            _gl = gl;

            uint vertexShader = CompileShader(ShaderType.VertexShader, vertexSource);
            uint fragmentShader = CompileShader(ShaderType.FragmentShader, fragmentSource);

            Handle = _gl.CreateProgram();
            _gl.AttachShader(Handle, vertexShader);
            _gl.AttachShader(Handle, fragmentShader);
            _gl.LinkProgram(Handle);

            _gl.GetProgram(Handle, GLEnum.LinkStatus, out var status);
            if (status == 0)
            {
                string infoLog = _gl.GetProgramInfoLog(Handle);
                throw new Exception($"Error linking shader program: {infoLog}");
            }

            _gl.DetachShader(Handle, vertexShader);
            _gl.DetachShader(Handle, fragmentShader);
            _gl.DeleteShader(vertexShader);
            _gl.DeleteShader(fragmentShader);
        }

        private uint CompileShader(ShaderType type, string source)
        {
            uint shader = _gl.CreateShader(type);
            _gl.ShaderSource(shader, source);
            _gl.CompileShader(shader);

            _gl.GetShader(shader, GLEnum.CompileStatus, out var status);
            if (status == 0)
            {
                string infoLog = _gl.GetShaderInfoLog(shader);
                throw new Exception($"Error compiling {type} shader:\n{infoLog}\nSource:\n{source}");
            }

            return shader;
        }

        public void Use()
        {
            _gl.UseProgram(Handle);
        }

        public int GetUniformLocation(string name)
        {
            return _gl.GetUniformLocation(Handle, name);
        }

        public unsafe void SetUniform(string name, Matrix4x4 matrix)
        {
            int location = GetUniformLocation(name);
            if (location != -1)
            {
                _gl.UniformMatrix4(location, 1, false, (float*)&matrix);
            }
        }

        public void SetUniform(string name, Vector3 value)
        {
            int location = GetUniformLocation(name);
            if (location != -1)
            {
                _gl.Uniform3(location, value.X, value.Y, value.Z);
            }
        }

        public void SetUniform(string name, float value)
        {
            int location = GetUniformLocation(name);
            if (location != -1)
            {
                _gl.Uniform1(location, value);
            }
        }

        public void SetUniform(string name, int value)
        {
            int location = GetUniformLocation(name);
            if (location != -1)
            {
                _gl.Uniform1(location, value);
            }
        }

        public void Dispose()
        {
            _gl.DeleteProgram(Handle);
        }
    }
}
