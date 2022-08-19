﻿using System;
using System.Collections.Generic;
using Antares.Graphics;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

using static Antares.Graphics.ARenderLayouts;
using static Antares.Graphics.ARenderUtilities;
using static Antares.Graphics.AShaderSpecifications;
using static Antares.Graphics.AShaderSpecifications.FluidSolverCompute;

namespace Antares.Physics
{
    public class APhysicsPipeline
    {
        public struct EmitterBufferBuilder : IDisposable
        {
            public int EmitterDispatchCount { get; private set; }

            public int PropertyByteCount { get; private set; }

            public int GroupParticleCount { get; private set; }

            public int TotalParticleCount { get; private set; }

            public NativeArray<FluidEmitterDispatch> EmitterDispatchBuffer => _emitterDispatchBuffer;

            public NativeArray<int> PartitionBuffer => _partitionBuffer;

            public NativeArray<byte> PropertyBuffer => _propertyBuffer;

            private NativeArray<FluidEmitterDispatch> _emitterDispatchBuffer;

            private NativeArray<int> _partitionBuffer;

            private NativeArray<byte> _propertyBuffer;

            private int _partitionCount;

            public unsafe void Reserve<T>(T emitter) where T : IFluidEmitter
            {
                GroupParticleCount += emitter.ParticleCount;
                if (GroupParticleCount > MaxEmitterParticleCountPerGroup)
                {
                    int groupCount = GroupParticleCount / MaxEmitterParticleCountPerGroup;
                    EmitterDispatchCount += groupCount;
                    GroupParticleCount = GroupParticleCount % MaxEmitterParticleCountPerGroup;
                }

                EmitterDispatchCount++;
                PropertyByteCount += emitter.PropertyByteCount;
            }

            public void Reserve<T>(List<T> emitters) where T : IFluidEmitter
            {
                for (int i = 0; i < emitters.Count; i++)
                    Reserve(emitters[i]);
            }

            public void Allocate()
            {
                _emitterDispatchBuffer = new NativeArray<FluidEmitterDispatch>(EmitterDispatchCount, Allocator.Temp);
                int partitionCount = (TotalParticleCount + MaxEmitterParticleCountPerGroup - 1) / MaxEmitterParticleCountPerGroup;
                _partitionBuffer = new NativeArray<int>(partitionCount + 1, Allocator.Temp);
                _partitionBuffer[0] = 0;
                _propertyBuffer = new NativeArray<byte>(PropertyByteCount, Allocator.Temp);

                EmitterDispatchCount = 0;
                PropertyByteCount = 0;
                GroupParticleCount = 0;
            }

            public void AddEmitter<T>(T emitter) where T : IFluidEmitter
            {
                int propertyByteOffset = PropertyByteCount;
                int propertyByteCount = emitter.PropertyByteCount;
                PropertyByteCount += propertyByteCount;

                emitter.GetProperties(_propertyBuffer.Slice(propertyByteOffset, propertyByteCount));

                int particleCount = emitter.ParticleCount;
                TotalParticleCount += particleCount;

                FluidEmitterType type = emitter.EmitterType;
                int groupParticleCount = GroupParticleCount + particleCount;
                while (groupParticleCount > MaxEmitterParticleCountPerGroup)
                {
                    _emitterDispatchBuffer[EmitterDispatchCount++] =
                        new FluidEmitterDispatch(type, propertyByteOffset, GroupParticleCount);
                    _partitionBuffer[++_partitionCount] = EmitterDispatchCount;

                    GroupParticleCount -= MaxEmitterParticleCountPerGroup;
                }

                _emitterDispatchBuffer[EmitterDispatchCount++] =
                    new FluidEmitterDispatch(emitter.EmitterType, propertyByteOffset, GroupParticleCount);
            }

            public void AddEmitter<T>(List<T> emitters) where T : IFluidEmitter
            {
                for (int i = 0; i < emitters.Count; i++)
                    AddEmitter(emitters[i]);
            }

            public void Submit()
            {
#if UNITY_EDITOR
                // todo
                Debug.Assert(_partitionBuffer.Length == _partitionCount + );
#endif

                _partitionBuffer[_partitionCount + 1] = EmitterDispatchCount;
            }

            public void Dispose()
            {
                _emitterDispatchBuffer.Dispose();
                _partitionBuffer.Dispose();
                _propertyBuffer.Dispose();
            }
        }

        public bool IsSceneLoaded { get; private set; } = false;

        public int MaxAddParticleCount { get => 1024; }

        private APhysicsScene _physicsScene;

        private ARenderPipeline _renderPipeline;
        private AShaderSpecifications _shaderSpecs;

        private ComputeBuffer _fluidParticlePositionsBuffer;
        private ComputeBuffer _fluidParticlePropertiesBuffer;
        private ComputeBuffer _fluidParticlePropertiesPoolBuffer;

        private RenderTexture _fluidGridLevel0;
        private RenderTexture _fluidGridLevel1;
        private RenderTexture _fluidGridLevel2;

        private ComputeBuffer _fluidBlockParticleOffsetsBuffer;

        private ComputeBuffer _partitionSumsBuffer;

        private ComputeBuffer _indirectArgsBuffer;

        private ComputeBuffer _fluidEmitterDispatchBuffer;
        private ComputeBuffer _fluidEmitterPartitionBuffer;
        private ComputeBuffer _fluidEmitterPropertyBuffer;

        public void LoadPhysicsScene(CommandBuffer cmd, APhysicsScene scene)
        {
            Debug.Assert(!IsSceneLoaded);
            Debug.Assert(scene);

            _physicsScene = scene;

            _renderPipeline = ARenderPipeline.Instance;
            _shaderSpecs = _renderPipeline.ShaderSpecs;

            #region allocate buffers/textures

            const int maxParticleCount = MaxParticleCount;
            _fluidParticlePositionsBuffer = new ComputeBuffer(4 + 2 * 4 * maxParticleCount, 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);
            _fluidParticlePropertiesBuffer = new ComputeBuffer(maxParticleCount * 8, 4, ComputeBufferType.Raw, ComputeBufferMode.Immutable);
            _fluidParticlePropertiesPoolBuffer = new ComputeBuffer(1 + maxParticleCount, 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);

            _fluidGridLevel0 = CreateRWVolumeRT(GraphicsFormat.R32_SInt, GridSizeLevel0);
            _fluidGridLevel1 = CreateRWVolumeRT(GraphicsFormat.R32_UInt, GridSizeLevel1);
            _fluidGridLevel2 = CreateRWVolumeRT(GraphicsFormat.R32_UInt, GridSizeLevel2);

            _fluidBlockParticleOffsetsBuffer = new ComputeBuffer(8 + BlockCountLevel0 * 8,
                4, ComputeBufferType.Structured | ComputeBufferType.IndirectArguments, ComputeBufferMode.Dynamic);

            _partitionSumsBuffer = new ComputeBuffer(1 + 2 * PrefixSumPartitionCount, 4, ComputeBufferType.Structured, ComputeBufferMode.Immutable);

            _indirectArgsBuffer = new ComputeBuffer(3 * 3 + 4, 4, ComputeBufferType.Structured | ComputeBufferType.IndirectArguments, ComputeBufferMode.Immutable);

            unsafe
            {
                _fluidEmitterDispatchBuffer = new ComputeBuffer(MaxFluidEmitterDispatchCount, sizeof(FluidEmitterDispatch), ComputeBufferType.Raw, ComputeBufferMode.Dynamic);
                _fluidEmitterPartitionBuffer = new ComputeBuffer(MaxFluidEmitterPartitionCount, 4, ComputeBufferType.Structured, ComputeBufferMode.Dynamic);
                _fluidEmitterPropertyBuffer = new ComputeBuffer(MaxFluidEmitterPropertyCount, 4, ComputeBufferType.Raw, ComputeBufferMode.Dynamic);
            }

            #endregion

            #region initialize buffers/textures

            // level 2 will be cleared at the beginning of every frame
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel0, 0);
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel1, 0);

            var parameters = new PhysicsSceneParameters(_renderPipeline.Scene, _physicsScene);
            ConstantBuffer.UpdateData(cmd, parameters);

            cmd.SetBufferData(_fluidParticlePositionsBuffer, new uint[] { 0, 0 });
            cmd.SetBufferData(_fluidParticlePropertiesPoolBuffer, new uint[] { 0 });
            cmd.SetBufferData(_fluidBlockParticleOffsetsBuffer, new uint[] { 0, 1, 1, 0, 1, 1 });
            cmd.SetBufferData(_partitionSumsBuffer, new uint[] { 0 });
            cmd.SetBufferData(_indirectArgsBuffer, new uint[] { 0, 1, 1, 0, 1, 1, 0, 1, 1, 1, 0, 0, 0 });

            cmd.SetGlobalBuffer(Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);

            ComputeShader shader = _shaderSpecs.FluidSolver.Shader;

            int kernel = _shaderSpecs.FluidSolver.ClearPartitionSumsKernel;
            int kernelSize = ClearPartitionSumsKernelSize;
            int groupCount = (PrefixSumPartitionCount + kernelSize - 1) / kernelSize;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.DispatchCompute(shader, kernel, groupCount, 1, 1);

            #endregion

            IsSceneLoaded = true;
        }

        public void UnloadPhysicsScene()
        {
            Debug.Assert(IsSceneLoaded);

            _shaderSpecs = null;
            _physicsScene = null;

            _fluidParticlePositionsBuffer.Release();
            _fluidParticlePropertiesBuffer.Release();
            _fluidParticlePropertiesPoolBuffer.Release();
            _fluidBlockParticleOffsetsBuffer.Release();
            _partitionSumsBuffer.Release();
            _indirectArgsBuffer.Release();
            _fluidEmitterDispatchBuffer.Release();
            _fluidEmitterPropertyBuffer.Release();
            _fluidEmitterPartitionBuffer.Release();

            _fluidGridLevel0.Release();
            _fluidGridLevel1.Release();
            _fluidGridLevel2.Release();

            IsSceneLoaded = false;
        }

        public void Solve(CommandBuffer cmd, float deltaTime, int currentFrameAddParticleCount)
        {
            _shaderSpecs.TextureUtilCS.ClearVolume(cmd, _fluidGridLevel2, 0);

            FluidSolverCompute fluidSolver = _shaderSpecs.FluidSolver;
            ComputeShader shader = fluidSolver.Shader;

            // set constant buffers
            {
                ConstantBuffer.Set<PhysicsSceneParameters>(cmd, shader, Bindings.PhysicsSceneParameters);

                var parameter = new PhysicsFrameParameters(_renderPipeline.Scene, _physicsScene, deltaTime, currentFrameAddParticleCount);
                ConstantBuffer.Push(cmd, parameter, shader, Bindings.PhysicsFrameParameters);
            }

            int kernel;

            kernel = fluidSolver.ClearFluidGridLevel0;
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);

            kernel = fluidSolver.ClearFluidGridLevel1;
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 12);

            kernel = fluidSolver.GenerateIndirectArgs0Kernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.IndirectArgs, _indirectArgsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePropertiesPool, _fluidParticlePropertiesPoolBuffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);

            kernel = fluidSolver.ClearPartitionSumsKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.DispatchCompute(shader, kernel, _indirectArgsBuffer, 24);

            kernel = fluidSolver.GenerateParticleHistogramKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePropertiesPool, _fluidParticlePropertiesPoolBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _indirectArgsBuffer, 0);

            kernel = fluidSolver.GenerateIndirectArgs1Kernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.IndirectArgs, _indirectArgsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);

            kernel = fluidSolver.GenerateParticleOffsetsKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.DispatchCompute(shader, kernel, _indirectArgsBuffer, 0);

            kernel = fluidSolver.SortParticlesKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _indirectArgsBuffer, 12);

            kernel = fluidSolver.GenerateIndirectArgs2Kernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.IndirectArgs, _indirectArgsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.PartitionSums, _partitionSumsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.DispatchCompute(shader, kernel, 1, 1, 1);

            kernel = fluidSolver.ParticleToGrid0Kernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);

            kernel = fluidSolver.ParticleToGrid1Kernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);

            kernel = fluidSolver.SolveGridLevel0Kernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.SceneVolume, _renderPipeline.SceneVolume);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);

            kernel = fluidSolver.SolveGridLevel1Kernel;
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 12);

            kernel = fluidSolver.GridToParticleKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidBlockParticleOffsets, _fluidBlockParticleOffsetsBuffer);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel0, _fluidGridLevel0);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel1, _fluidGridLevel1);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.FluidGridLevel2, _fluidGridLevel2);
            cmd.SetComputeTextureParam(shader, kernel, Bindings.SceneVolume, _renderPipeline.SceneVolume);
            cmd.DispatchCompute(shader, kernel, _fluidBlockParticleOffsetsBuffer, 0);
        }

        public void AddParticles(CommandBuffer cmd, EmitterBufferBuilder emitterBufferBuilder)
        {
            if (emitterBufferBuilder.TotalParticleCount == 0)
                return;

            cmd.SetBufferData(_fluidEmitterDispatchBuffer, emitterBufferBuilder.EmitterDispatchBuffer);
            cmd.SetBufferData(_fluidEmitterPartitionBuffer, emitterBufferBuilder.PartitionBuffer);
            cmd.SetBufferData(_fluidEmitterPropertyBuffer, emitterBufferBuilder.PropertyBuffer);

            FluidSolverCompute fluidEmitter = _shaderSpecs.FluidSolver;
            ComputeShader shader = fluidEmitter.Shader;
            AddParticlesParameters parameters = new AddParticlesParameters(mass: 1f);
            ConstantBuffer.Push(cmd, parameters, shader, Bindings.AddParticlesParameters);

            int kernel = fluidEmitter.AddParticlesKernel;
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidEmitterDispatches, _fluidEmitterDispatchBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidEmitterPartitions, _fluidEmitterPartitionBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidEmitterProperties, _fluidEmitterPropertyBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePositions, _fluidParticlePositionsBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticleProperties, _fluidParticlePropertiesBuffer);
            cmd.SetComputeBufferParam(shader, kernel, Bindings.FluidParticlePropertiesPool, _fluidParticlePropertiesPoolBuffer);
            cmd.DispatchCompute(shader, kernel, emitterBufferBuilder.PartitionCount, 1, 1);
        }

        public void RenderDebugParticles(CommandBuffer cmd, Camera camera, float particleSize = .25f)
        {
            DebugFluidParticleGraphics debugFluidParticle = _shaderSpecs.DebugFluidParticle;

            var parameters = new DebugFluidParticleGraphics.DebugFluidParticleParameters(
                camera.transform.position, camera.transform.up * particleSize);
            ConstantBuffer.PushGlobal(parameters, Bindings.DebugFluidParticleParameters);

            cmd.DrawProceduralIndirect(Matrix4x4.identity, debugFluidParticle.Material, 0,
                MeshTopology.Points, _indirectArgsBuffer, 36);
        }
    }
}
