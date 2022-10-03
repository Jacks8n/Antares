using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;

namespace Antares.Graphics
{
    public class DebugBuffer : IDisposable
    {
        public const int MaxDebugLogCount = 512;

        private enum LogType
        {
            Int, UInt, Float, Bool,
            Int2, UInt2, Float2, Bool2,
            Int3, UInt3, Float3, Bool3,
            Int4, UInt4, Float4, Bool4
        }

        [StructLayout(LayoutKind.Explicit, Pack = 1)]
        private struct DebugLog
        {
            [FieldOffset(0)]
            public LogType Type;

            [FieldOffset(0)]
            public int WordCount;

            [FieldOffset(0)]
            public int IntValue0;

            [FieldOffset(0)]
            public uint UIntValue0;

            [FieldOffset(0)]
            public float FloatValue0;

            [FieldOffset(4)]
            public int IntValue1;

            [FieldOffset(4)]
            public uint UIntValue1;

            [FieldOffset(4)]
            public float FloatValue1;
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
            _debugBuffer.SetData(new DebugLog[] { new DebugLog() { WordCount = 0 } });
        }

        public void Reset(CommandBuffer cmd)
        {
            cmd.SetBufferData(_debugBuffer, new DebugLog[] { new DebugLog() { WordCount = 0 } });
        }

        public DebugBuffer Read()
        {
            _debugBuffer.GetData(_logData);
            return this;
        }

        public int ForEach(Action<object> action)
        {
            int wordCount = _logData[0].WordCount;
            if (wordCount > MaxDebugLogCount)
                wordCount = MaxDebugLogCount;

            int logCount = 0;
            for (int i = 1; i < wordCount + 1; i++)
            {
                // skip the first element which is used as a counter
                DebugLog log = _logData[i];

                switch (log.Type)
                {
                    case LogType.Int:
                        action(log.IntValue1);
                        break;
                    case LogType.UInt:
                    case LogType.Bool:
                        action(log.UIntValue1);
                        break;
                    case LogType.Float:
                        action(log.FloatValue1);
                        break;
                    case LogType.Int2:
                    case LogType.UInt2:
                    case LogType.Bool2:
                        action(new Vector2Int(log.IntValue1, _logData[++i].IntValue0));
                        break;
                    case LogType.Float2:
                        action(new Vector2(log.FloatValue1, _logData[++i].FloatValue0));
                        break;
                    case LogType.Int3:
                    case LogType.UInt3:
                    case LogType.Bool3:
                        action(new Vector3Int(log.IntValue1, _logData[++i].IntValue0, _logData[i].IntValue1));
                        break;
                    case LogType.Float3:
                        action(new Vector3(log.FloatValue1, _logData[++i].FloatValue0, _logData[i].FloatValue1));
                        break;
                    case LogType.Int4:
                    case LogType.UInt4:
                    case LogType.Bool4:
                        action(new Vector4(log.IntValue1, _logData[++i].IntValue0, _logData[i].IntValue1, _logData[++i].IntValue0));
                        break;
                    case LogType.Float4:
                        action(new Vector4(log.FloatValue1, _logData[++i].FloatValue0, _logData[i].FloatValue1, _logData[++i].FloatValue0));
                        break;
                    default:
                        continue;
                }

                logCount++;
            }

            return logCount;
        }

        public int PrintAll()
        {
            return ForEach(value => Debug.Log(value));
        }
    }
}
