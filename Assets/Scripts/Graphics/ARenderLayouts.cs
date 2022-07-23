using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;

namespace Antares.Graphics
{
    public static class ARenderLayouts
    {
        public static class Bindings
        {
            public static int MaterialVolume { get; } = Shader.PropertyToID(nameof(MaterialVolume));

            public static int SceneVolume { get; } = Shader.PropertyToID(nameof(SceneVolume));

            public static int MaterialVolumeMip { get; } = Shader.PropertyToID(nameof(MaterialVolumeMip));

            public static int SceneVolumeMip { get; } = Shader.PropertyToID(nameof(SceneVolumeMip));

            public static int VolumeMipLevel { get; } = Shader.PropertyToID(nameof(VolumeMipLevel));

            public static int TiledRM { get; } = Shader.PropertyToID(nameof(TiledRM));

            public static int FrameBufferSize { get; } = Shader.PropertyToID(nameof(FrameBufferSize));

            public static int Depth { get; } = Shader.PropertyToID(nameof(Depth));

            public static int Shading { get; } = Shader.PropertyToID(nameof(Shading));

            public static int SceneRM0 { get; } = Shader.PropertyToID(nameof(SceneRM0));

            public static int SceneRM1 { get; } = Shader.PropertyToID(nameof(SceneRM1));

            public static int RayMarchingParameters { get; } = Shader.PropertyToID(nameof(RayMarchingParameters));

            public static int Source { get; } = Shader.PropertyToID(nameof(Source));

            public static int Destination { get; } = Shader.PropertyToID(nameof(Destination));

            public static int DestinationInt { get; } = Shader.PropertyToID(nameof(DestinationInt));

            public static int DestinationBuffer { get; } = Shader.PropertyToID(nameof(DestinationBuffer));

            public static int Bound { get; } = Shader.PropertyToID(nameof(Bound));

            public static int Offset { get; } = Shader.PropertyToID(nameof(Offset));

            public static int Value { get; } = Shader.PropertyToID(nameof(Value));

            public static int ValueInt { get; } = Shader.PropertyToID(nameof(ValueInt));

            public static int SDFBrushes { get; } = Shader.PropertyToID(nameof(SDFBrushes));

            public static int BrushParameters { get; } = Shader.PropertyToID(nameof(BrushParameters));

            public static int DispatchCoords { get; } = Shader.PropertyToID(nameof(DispatchCoords));

            public static int BrushIndices { get; } = Shader.PropertyToID(nameof(BrushIndices));

            public static int MipDispatches { get; } = Shader.PropertyToID(nameof(MipDispatches));

            public static int SDFGenerationParameters { get; } = Shader.PropertyToID(nameof(SDFGenerationParameters));

            public static int MipGenerationParameters { get; } = Shader.PropertyToID(nameof(MipGenerationParameters));

            public static int BrushAtlas { get; } = Shader.PropertyToID(nameof(BrushAtlas));

            public static int FluidParticlePositions { get; } = Shader.PropertyToID(nameof(FluidParticlePositions));

            public static int FluidParticleProperties { get; } = Shader.PropertyToID(nameof(FluidParticleProperties));

            public static int FluidParticlePropertiesPool { get; } = Shader.PropertyToID(nameof(FluidParticlePropertiesPool));

            public static int FluidGridLevel0 { get; } = Shader.PropertyToID(nameof(FluidGridLevel0));

            public static int FluidGridLevel1 { get; } = Shader.PropertyToID(nameof(FluidGridLevel1));

            public static int FluidGridLevel2 { get; } = Shader.PropertyToID(nameof(FluidGridLevel2));

            public static int FluidBlockParticleOffsets { get; } = Shader.PropertyToID(nameof(FluidBlockParticleOffsets));

            public static int IndirectArgs { get; } = Shader.PropertyToID(nameof(IndirectArgs));

            public static int IndirectArgsOffset { get; } = Shader.PropertyToID(nameof(IndirectArgsOffset));

            public static int ParticlesToAdd { get; } = Shader.PropertyToID(nameof(ParticlesToAdd));

            public static int PartitionSums { get; } = Shader.PropertyToID(nameof(PartitionSums));

            public static int PhysicsSceneParameters { get; } = Shader.PropertyToID(nameof(PhysicsSceneParameters));

            public static int PhysicsFrameParameters { get; } = Shader.PropertyToID(nameof(PhysicsFrameParameters));

            public static int AddParticlesParameters { get; } = Shader.PropertyToID(nameof(AddParticlesParameters));

            public static int DebugFluidParticleParameters { get; } = Shader.PropertyToID(nameof(DebugFluidParticleParameters));

            public static readonly int[] NonAttachmentRTs = new int[] {
                TiledRM,
                Shading,
                SceneRM0,
                SceneRM1
            };
        }

        public static class Attachments
        {
            public static RenderTextureFormat Format_Depth { get => RenderTextureFormat.Depth; }

            public static GraphicsFormat Format_GBuffer0 { get => GraphicsFormat.R16G16B16A16_SFloat; }

            public static GraphicsFormat Format_Shading { get => GraphicsFormat.R16G16B16A16_SFloat; }
        }

        public static class RenderPass0
        {
            private enum Enum { Depth, GBuffer0, Shading, Max };

            public const int AttachmentCount = (int)Enum.Max;
            public const int Index_Depth = (int)Enum.Depth;
            public const int Index_GBuffer0 = (int)Enum.GBuffer0;
            public const int Index_Shading = (int)Enum.Shading;

            public static NativeArray<AttachmentDescriptor> GetAttachments(Color clearColor)
            {
                var attachments = new NativeArray<AttachmentDescriptor>(AttachmentCount, Allocator.Temp);

                attachments[Index_Depth] = new AttachmentDescriptor(Attachments.Format_Depth)
                {
                    loadAction = RenderBufferLoadAction.Clear,
                    storeAction = RenderBufferStoreAction.DontCare,
                    clearDepth = 1f
                };
                attachments[Index_GBuffer0] = new AttachmentDescriptor(Attachments.Format_GBuffer0)
                {
                    loadAction = RenderBufferLoadAction.Clear,
                    storeAction = RenderBufferStoreAction.DontCare,
                    clearColor = Color.clear
                };
                attachments[Index_Shading] = new AttachmentDescriptor(Attachments.Format_Shading)
                {
                    loadStoreTarget = new RenderTargetIdentifier(Bindings.Shading),
                    loadAction = RenderBufferLoadAction.DontCare,
                    storeAction = RenderBufferStoreAction.Store,
                    clearColor = clearColor
                };

                return attachments;
            }
        }
    }
}
