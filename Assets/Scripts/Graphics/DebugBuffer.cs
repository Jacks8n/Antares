using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;

namespace Antares.Graphics
{
    public class DebugBuffer : IDisposable
    {
        public const int MaxDebugLogCount = 128;

        private enum LogType { Int, UInt, Float, Bool }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private struct DebugLog
        {
            [FieldOffset(0)]
            public LogType Type;

            [FieldOffset(0)]
            public int LogCount;

            [FieldOffset(4)]
            public int IntValue;

            [FieldOffset(4)]
            public uint UIntValue;

            [FieldOffset(4)]
            public float FloatValue;
        }

        private readonly ComputeBuffer _debugBuffer;

        private readonly DebugLog[] _logData;

        public unsafe DebugBuffer()
        {
#if !UNITY_EDITOR
            Debug.LogWarning($"{nameof(DebugBuffer)} is Expected to be Used in Debug Mode Only");
#endif

            _debugBuffer = new ComputeBuffer(MaxDebugLogCount + 1, sizeof(DebugLog), ComputeBufferType.Raw);
            _logData = new DebugLog[MaxDebugLogCount + 1];

            Reset();
        }

        ~DebugBuffer()
        {
            Dispose();
        }

        public void Dispose()
        {
            _debugBuffer.Dispose();
        }

        public void SetGlobalParam()
        {
            Shader.SetGlobalBuffer(Bindings.DebugBuffer, _debugBuffer);
        }

        public void SetGlobalParam(CommandBuffer cmd)
        {
            cmd.SetGlobalBuffer(Bindings.DebugBuffer, _debugBuffer);
        }

        public void SetParam(ComputeShader shader, int kernel)
        {
            shader.SetBuffer(kernel, Bindings.DebugBuffer, _debugBuffer);
        }

        public void SetParam(CommandBuffer cmd, ComputeShader shader, int kernel)
        {
            cmd.SetComputeBufferParam(shader, kernel, Bindings.DebugBuffer, _debugBuffer);
        }

        public void Reset()
        {
            _debugBuffer.SetData(new DebugLog[] { new DebugLog() { LogCount = 0 } });
        }

        public void Reset(CommandBuffer cmd)
        {
            cmd.SetBufferData(_debugBuffer, new DebugLog[] { new DebugLog() { LogCount = 0 } });
        }

        public void Read()
        {
            _debugBuffer.GetData(_logData);
        }

        public bool ForEach(Action<double> action)
        {
            int count = _logData[0].LogCount;
            if (count > MaxDebugLogCount)
                count = MaxDebugLogCount;

            for (int i = 0; i < count; i++)
            {
                // skip the first element which is used as a counter
                DebugLog log = _logData[i + 1];

                switch (log.Type)
                {
                    case LogType.Int:
                        action(log.IntValue);
                        break;
                    case LogType.UInt:
                    case LogType.Bool:
                        action(log.UIntValue);
                        break;
                    case LogType.Float:
                        action(log.FloatValue);
                        break;
                }
            }

            return count > 0;
        }

        public bool PrintAll()
        {
            return ForEach(value => Debug.Log(value));
        }
    }
}
