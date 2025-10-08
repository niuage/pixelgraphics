using System.Collections.Generic;
using Aarthificial.PixelGraphics.Common;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

namespace Aarthificial.PixelGraphics.Forward
{
    public class VelocityRenderPass : ScriptableRenderPass
    {
        private class PassData
        {
            internal Material emitterMaterial;
            internal Material blitMaterial;
            internal TextureHandle velocityTexture;
            internal TextureHandle previousVelocityTexture;
            internal TextureHandle temporaryVelocityTexture;
            internal VelocityPassSettings passSettings;
            internal SimulationSettings simulationSettings;
            internal Vector2 cameraPositionDelta;
            internal Vector4 pixelScreenParams;
            internal Matrix4x4 viewMatrix;
            internal Matrix4x4 projectionMatrix;
            internal int textureWidth;
            internal int textureHeight;
            internal bool isPreviewCamera;
            internal bool isSceneViewCamera;
            internal RendererListHandle rendererListHandleLayerMask;
            internal RendererListHandle rendererListHandleRenderingLayerMask;
            internal bool hasLayerMask;
            internal bool hasRenderingLayerMask;
        }

        private readonly List<ShaderTagId> _shaderTagIdList = new List<ShaderTagId>();
        private readonly ProfilingSampler _profilingSampler;
        private readonly Material _emitterMaterial;
        private readonly Material _blitMaterial;

        private VelocityPassSettings _passSettings;
        private SimulationSettings _simulationSettings;
        private FilteringSettings _filteringSettings;
        private Vector2 _previousPosition;

        // RTHandles for persistent velocity textures (double buffering)
        private RTHandle _velocityTargetA;
        private RTHandle _velocityTargetB;
        private bool _useTargetA = true;

        public VelocityRenderPass(Material emitterMaterial, Material blitMaterial)
        {
            _emitterMaterial = emitterMaterial;
            _blitMaterial = blitMaterial;

            _shaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
            _shaderTagIdList.Add(new ShaderTagId("UniversalForward"));
            _shaderTagIdList.Add(new ShaderTagId("Universal2D"));
            _shaderTagIdList.Add(new ShaderTagId("UniversalForwardOnly"));
            _shaderTagIdList.Add(new ShaderTagId("LightweightForward"));

            _filteringSettings = new FilteringSettings(RenderQueueRange.transparent);
            _profilingSampler = new ProfilingSampler(nameof(VelocityRenderPass));

            renderPassEvent = RenderPassEvent.BeforeRenderingOpaques;
        }

        public void Dispose()
        {
            _velocityTargetA?.Release();
            _velocityTargetB?.Release();
        }

        public void Setup(
            VelocityPassSettings passSettings,
            SimulationSettings simulationSettings
        )
        {
            _passSettings = passSettings;
            _simulationSettings = simulationSettings;
        }

        private static void ExecutePass(PassData data, RasterGraphContext context)
        {
            var cmd = context.cmd;

            Debug.Log($"[VelocityRenderPass] ExecutePass called - rendering velocity simulation");

            // Set global shader parameters
            cmd.SetGlobalVector(ShaderIds.CameraPositionDelta, data.cameraPositionDelta);
            cmd.SetGlobalVector(ShaderIds.VelocitySimulationParams, data.simulationSettings.Value);
            cmd.SetGlobalVector(ShaderIds.PixelScreenParams, data.pixelScreenParams);

            // Set the previous velocity texture so the shader can read it
            cmd.SetGlobalTexture(ShaderIds.PreviousVelocityTexture, data.previousVelocityTexture);

            // Set the temporary velocity texture (current emitter data)
            cmd.SetGlobalTexture(ShaderIds.TemporaryVelocityTexture, data.temporaryVelocityTexture);

            // Blit using fullscreen quad to apply velocity simulation
            cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
            cmd.SetViewport(new Rect(0, 0, data.textureWidth, data.textureHeight));
            cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, data.blitMaterial, 0, 0);
            cmd.SetViewProjectionMatrices(data.viewMatrix, data.projectionMatrix);
        }

        private static void ExecuteRendererLists(PassData data, RasterGraphContext context)
        {
            var cmd = context.cmd;

            Debug.Log($"[VelocityRenderPass] ExecuteRendererLists called - hasLayerMask: {data.hasLayerMask}, hasRenderingLayerMask: {data.hasRenderingLayerMask}");

            // Set shader parameter for emitters
            cmd.SetGlobalVector(ShaderIds.PositionDelta, data.cameraPositionDelta);

            if (!data.isPreviewCamera && !data.isSceneViewCamera)
            {
                if (data.hasLayerMask)
                {
                    cmd.DrawRendererList(data.rendererListHandleLayerMask);
                }

                if (data.hasRenderingLayerMask)
                {
                    cmd.DrawRendererList(data.rendererListHandleRenderingLayerMask);
                }
            }
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalRenderingData renderingData = frameData.Get<UniversalRenderingData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            int textureWidth = Mathf.FloorToInt(cameraData.camera.pixelWidth * _passSettings.textureScale);
            int textureHeight = Mathf.FloorToInt(cameraData.camera.pixelHeight * _passSettings.textureScale);

            // Debug: Log when passes are being recorded
            Debug.Log($"[VelocityRenderPass] RecordRenderGraph called - Preview: {_passSettings.preview}, LayerMask: {_passSettings.layerMask.value}, RenderingLayerMask: {(uint)_passSettings.renderingLayerMask}");

            float height = 2 * cameraData.camera.orthographicSize * _passSettings.pixelsPerUnit;
            float width = height * cameraData.camera.aspect;

            var cameraPosition = (Vector2)cameraData.GetViewMatrix().GetColumn(3);
            var delta = cameraPosition - _previousPosition;
            var screenDelta = cameraData.GetProjectionMatrix() * cameraData.GetViewMatrix() * delta;
            _previousPosition = cameraPosition;

            // Create texture descriptor
            var desc = new RenderTextureDescriptor(
                textureWidth,
                textureHeight,
                GraphicsFormat.R16G16B16A16_SFloat,
                0
            );
            desc.msaaSamples = 1;
            desc.enableRandomWrite = false;

            // Allocate or reallocate RTHandles for double buffering
            RenderingUtils.ReAllocateHandleIfNeeded(ref _velocityTargetA, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_PG_VelocityTarget_A");
            RenderingUtils.ReAllocateHandleIfNeeded(ref _velocityTargetB, desc, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_PG_VelocityTarget_B");

            // Determine which targets to use for this frame
            RTHandle currentVelocityTarget = _useTargetA ? _velocityTargetA : _velocityTargetB;
            RTHandle previousVelocityTarget = _useTargetA ? _velocityTargetB : _velocityTargetA;

            // Flip for next frame
            _useTargetA = !_useTargetA;

            // Import the RTHandles into the render graph
            TextureHandle currentVelocityHandle = renderGraph.ImportTexture(currentVelocityTarget);
            TextureHandle previousVelocityHandle = renderGraph.ImportTexture(previousVelocityTarget);

            // Create a temporary texture for emitter data
            var tempVelocityDesc = new TextureDesc(textureWidth, textureHeight)
            {
                colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
                clearBuffer = true,
                clearColor = Color.clear,
                name = "Temporary Velocity"
            };
            TextureHandle temporaryVelocityHandle = renderGraph.CreateTexture(tempVelocityDesc);

            // First pass: Draw emitters (write velocity data to temporary texture)
            if (!cameraData.isPreviewCamera && !cameraData.isSceneViewCamera)
            {
                bool hasLayerMask = _passSettings.layerMask != 0;
                bool hasRenderingLayerMask = _passSettings.renderingLayerMask != 0;

                Debug.Log($"[VelocityRenderPass] Emitter check - hasLayerMask: {hasLayerMask}, hasRenderingLayerMask: {hasRenderingLayerMask}");

                if (hasLayerMask || hasRenderingLayerMask)
                {
                    using (var builder = renderGraph.AddRasterRenderPass<PassData>("Velocity Emitters", out var passData, _profilingSampler))
                    {
                        passData.emitterMaterial = _emitterMaterial;
                        passData.passSettings = _passSettings;
                        passData.cameraPositionDelta = screenDelta / 2;
                        passData.isPreviewCamera = cameraData.isPreviewCamera;
                        passData.isSceneViewCamera = cameraData.isSceneViewCamera;
                        passData.hasLayerMask = hasLayerMask;
                        passData.hasRenderingLayerMask = hasRenderingLayerMask;

                        // Create renderer lists for emitters
                        if (hasLayerMask)
                        {
                            var filteringSettings = _filteringSettings;
                            filteringSettings.layerMask = _passSettings.layerMask;
                            filteringSettings.renderingLayerMask = uint.MaxValue;

                            var drawSettings = RenderingUtils.CreateDrawingSettings(_shaderTagIdList, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                            drawSettings.overrideMaterial = null;
                            drawSettings.overrideMaterialPassIndex = 0;

                            var rendererListParams = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                            passData.rendererListHandleLayerMask = renderGraph.CreateRendererList(rendererListParams);
                            builder.UseRendererList(passData.rendererListHandleLayerMask);
                        }

                        if (hasRenderingLayerMask)
                        {
                            var filteringSettings = _filteringSettings;
                            filteringSettings.layerMask = -1;
                            filteringSettings.renderingLayerMask = _passSettings.renderingLayerMask;

                            var drawSettings = RenderingUtils.CreateDrawingSettings(_shaderTagIdList, renderingData, cameraData, lightData, SortingCriteria.CommonTransparent);
                            drawSettings.overrideMaterial = _emitterMaterial;
                            drawSettings.overrideMaterialPassIndex = 0;

                            var rendererListParams = new RendererListParams(renderingData.cullResults, drawSettings, filteringSettings);
                            passData.rendererListHandleRenderingLayerMask = renderGraph.CreateRendererList(rendererListParams);
                            builder.UseRendererList(passData.rendererListHandleRenderingLayerMask);
                        }

                        // Write to TEMPORARY velocity texture (emitters write their velocity here)
                        builder.SetRenderAttachment(temporaryVelocityHandle, 0, AccessFlags.Write);

                        // Set as global texture so simulation can read it
                        builder.SetGlobalTextureAfterPass(temporaryVelocityHandle, ShaderIds.TemporaryVelocityTexture);

                        // Allow setting global shader variables in this pass
                        builder.AllowGlobalStateModification(true);

                        builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecuteRendererLists(data, context));
                    }
                }
            }

            // Second pass: Simulate velocity (combine previous frame + current emitter data)
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("Velocity Simulation", out var passData, _profilingSampler))
            {
                passData.blitMaterial = _blitMaterial;
                passData.emitterMaterial = _emitterMaterial;
                passData.passSettings = _passSettings;
                passData.simulationSettings = _simulationSettings;
                passData.cameraPositionDelta = screenDelta / 2;
                passData.pixelScreenParams = new Vector4(width, height, _passSettings.pixelsPerUnit, 1 / _passSettings.pixelsPerUnit);
                passData.viewMatrix = cameraData.GetViewMatrix();
                passData.projectionMatrix = cameraData.GetProjectionMatrix();
                passData.textureWidth = textureWidth;
                passData.textureHeight = textureHeight;
                passData.isPreviewCamera = cameraData.isPreviewCamera;
                passData.isSceneViewCamera = cameraData.isSceneViewCamera;
                passData.previousVelocityTexture = previousVelocityHandle;
                passData.temporaryVelocityTexture = temporaryVelocityHandle;

                // Read from previous velocity texture (last frame's result)
                builder.UseTexture(previousVelocityHandle, AccessFlags.Read);

                // Read from temporary velocity texture (current emitter data)
                builder.UseTexture(temporaryVelocityHandle, AccessFlags.Read);

                // Write to current velocity texture (simulation output)
                builder.SetRenderAttachment(currentVelocityHandle, 0, AccessFlags.Write);

                // Set velocity textures as global AFTER this pass completes
                builder.SetGlobalTextureAfterPass(currentVelocityHandle, ShaderIds.VelocityTexture);

                // Allow setting global shader variables in this pass
                builder.AllowGlobalStateModification(true);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
            }

#if UNITY_EDITOR
            // Preview pass (editor only)
            if (_passSettings.preview)
            {
                using (var builder = renderGraph.AddRasterRenderPass<PassData>("Velocity Preview Blit", out var passData, _profilingSampler))
                {
                    passData.blitMaterial = _blitMaterial;
                    passData.textureWidth = cameraData.camera.pixelWidth;  // Use screen width, not texture width
                    passData.textureHeight = cameraData.camera.pixelHeight; // Use screen height, not texture height
                    passData.viewMatrix = cameraData.GetViewMatrix();
                    passData.projectionMatrix = cameraData.GetProjectionMatrix();

                    // Use the velocity texture as input (this prevents it from being culled)
                    builder.UseTexture(currentVelocityHandle, AccessFlags.Read);

                    // Write to camera color target
                    builder.SetRenderAttachment(resourceData.activeColorTexture, 0, AccessFlags.Write);

                    // Allow pass culling to be disabled for debugging
                    builder.AllowPassCulling(false);

                    // Allow global state modification
                    builder.AllowGlobalStateModification(true);

                    builder.SetRenderFunc((PassData data, RasterGraphContext context) =>
                    {
                        Debug.Log("[VelocityRenderPass] Preview pass executing");
                        var cmd = context.cmd;

                        // Draw fullscreen quad with the velocity visualization (pass 1 of blit shader)
                        cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                        cmd.SetViewport(new Rect(0, 0, data.textureWidth, data.textureHeight));
                        cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, data.blitMaterial, 0, 1);
                        cmd.SetViewProjectionMatrices(data.viewMatrix, data.projectionMatrix);
                    });
                }
            }
#endif
        }
    }
}
