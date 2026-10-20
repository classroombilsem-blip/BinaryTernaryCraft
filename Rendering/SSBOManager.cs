using System;
using Silk.NET.OpenGL;

namespace BinaryTernaryCraft.Rendering
{
    public class SSBOManager : IDisposable
    {
        private readonly GL _gl;
        public uint StateBufferHandle { get; private set; }
        public uint KineticBufferHandle { get; private set; }

        public readonly int MaxLogicStates;
        public readonly int MaxKineticNetworks;

        public SSBOManager(GL gl, int maxLogicStates, int maxKineticNetworks = 4096)
        {
            _gl = gl;
            MaxLogicStates = maxLogicStates;
            MaxKineticNetworks = maxKineticNetworks;

            InitializeBuffers();
        }

        private unsafe void InitializeBuffers()
        {
            // 1. Logic State SSBO (Binding 0)
            StateBufferHandle = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, StateBufferHandle);
            
            // Allocate memory: Each voxel logic state is stored as 32-bit int (std430 4-byte scalar alignment)
            nuint stateByteSize = (nuint)(MaxLogicStates * sizeof(int));
            _gl.BufferData(BufferTargetARB.ShaderStorageBuffer, stateByteSize, null, BufferUsageARB.DynamicDraw);
            _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, StateBufferHandle);

            // 2. Kinetic Network Speeds SSBO (Binding 1)
            KineticBufferHandle = _gl.GenBuffer();
            _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, KineticBufferHandle);
            
            nuint kineticByteSize = (nuint)(MaxKineticNetworks * sizeof(float));
            _gl.BufferData(BufferTargetARB.ShaderStorageBuffer, kineticByteSize, null, BufferUsageARB.DynamicDraw);
            _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, KineticBufferHandle);

            _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
        }

        public unsafe void UpdateAllStates(int[] states)
        {
            if (states == null || states.Length == 0) return;

            _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, StateBufferHandle);
            fixed (int* ptr = states)
            {
                nuint bytesToCopy = (nuint)(Math.Min(states.Length, MaxLogicStates) * sizeof(int));
                _gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, 0, bytesToCopy, ptr);
            }
            _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
        }

        public unsafe void UpdateSingleState(uint globalIndex, int state)
        {
            if (globalIndex >= (uint)MaxLogicStates) return;

            _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, StateBufferHandle);
            int* ptr = &state;
            _gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, (nint)(globalIndex * sizeof(int)), (nuint)sizeof(int), ptr);
            _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
        }

        public unsafe void UpdateKineticSpeeds(float[] speeds)
        {
            if (speeds == null || speeds.Length == 0) return;

            _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, KineticBufferHandle);
            fixed (float* ptr = speeds)
            {
                nuint bytesToCopy = (nuint)(Math.Min(speeds.Length, MaxKineticNetworks) * sizeof(float));
                _gl.BufferSubData(BufferTargetARB.ShaderStorageBuffer, 0, bytesToCopy, ptr);
            }
            _gl.BindBuffer(BufferTargetARB.ShaderStorageBuffer, 0);
        }

        public void Bind()
        {
            _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 0, StateBufferHandle);
            _gl.BindBufferBase(BufferTargetARB.ShaderStorageBuffer, 1, KineticBufferHandle);
        }

        public void Dispose()
        {
            if (StateBufferHandle != 0)
            {
                _gl.DeleteBuffer(StateBufferHandle);
                StateBufferHandle = 0;
            }
            if (KineticBufferHandle != 0)
            {
                _gl.DeleteBuffer(KineticBufferHandle);
                KineticBufferHandle = 0;
            }
        }
    }
}
