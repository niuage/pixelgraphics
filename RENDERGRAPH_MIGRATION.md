# Render Graph Migration Guide

This document describes the migration from the legacy URP rendering API to the new Render Graph API in Unity 6000.2.

## Summary of Changes

The VelocityRenderPass and VelocityRenderFeature have been updated to use Unity's new Render Graph system introduced in URP for Unity 6. This migration improves performance, enables better optimization, and follows Unity's recommended practices for modern URP rendering.

## Key Changes

### VelocityRenderPass.cs

#### 1. **New Imports**
- Added `UnityEngine.Rendering.RenderGraphModule` namespace for Render Graph API support

#### 2. **PassData Class**
- Created a new `PassData` class to hold all data needed by the render functions
- This class is passed to the static render functions during execution
- Contains materials, texture handles, settings, camera matrices, and renderer list handles

#### 3. **Removed Legacy RTHandle Management**
- Removed `_temporaryVelocityTarget` and `_velocityTarget` RTHandle fields
- Removed direct shader property IDs (`_temporaryVelocityTargetId`, `_velocityTargetId`)

#### 4. **Added Proper Double Buffering**
- Implemented proper double buffering with `_velocityTargetA` and `_velocityTargetB`
- Added `_useTargetA` boolean to toggle between buffers each frame
- This replaces the TODO comment about implementing proper double buffering

#### 5. **RecordRenderGraph Method**
- Replaced the `Execute(ScriptableRenderContext, ref RenderingData)` method with `RecordRenderGraph(RenderGraph, ContextContainer)`
- The new method records rendering commands into the render graph instead of executing them directly
- Uses `ContextContainer` to access frame data (`UniversalResourceData`, `UniversalCameraData`, etc.)

#### 6. **Texture Management**
- Uses `RenderingUtils.ReAllocateHandleIfNeeded()` to manage RTHandles efficiently
- Imports RTHandles into the render graph using `renderGraph.ImportTexture()`
- Returns `TextureHandle` instances that are valid only for the current frame
- Sets global textures using `renderGraph.SetGlobalTextureAfterPass()` instead of `cmd.SetGlobalTexture()`

#### 7. **Render Passes**
- Split rendering into multiple render graph passes:
  - **Velocity Simulation Pass**: Applies velocity simulation using the blit shader
  - **Velocity Emitters Pass**: Renders emitters that write velocity data
  - **Preview Pass** (Editor only): Blits velocity texture to screen for debugging

#### 8. **Static Render Functions**
- Created static methods `ExecutePass()` and `ExecuteRendererLists()`
- These are called by the render graph during execution
- Receive `PassData` and `RasterGraphContext` as parameters

#### 9. **RendererList Creation**
- Replaced `ScriptableRenderContext.DrawRenderers()` with `RendererListHandle`
- Created renderer lists using `renderGraph.CreateRendererList()` with `RendererListDesc`
- Used `builder.UseRendererList()` to declare renderer list dependencies
- Executed renderer lists using `cmd.DrawRendererList()` in the render function

#### 10. **Resource Declaration**
- Used `builder.UseTexture()` to declare texture reads
- Used `builder.SetRenderAttachment()` to declare render targets
- The render graph uses these declarations to optimize pass execution and merging

#### 11. **Blit Operations**
- Replaced `cmd.Blit()` with `renderGraph.AddBlitPass()` for the preview feature
- Used `RenderGraphUtils.BlitMaterialParameters` to configure blit operations

#### 12. **Disposal**
- Added `Dispose()` method to release RTHandles when the pass is destroyed
- This prevents memory leaks

### VelocityRenderFeature.cs

#### 1. **Dispose Override**
- Implemented `protected override void Dispose(bool disposing)` method
- Properly disposes of the render pass and materials
- Uses `CoreUtils.Destroy()` to destroy materials

#### 2. **SetupRenderPasses**
- The `ConfigureTarget()` call remains but may not be necessary in Render Graph mode
- Kept for potential compatibility with Compatibility Mode

## Benefits of Render Graph

1. **Automatic Optimization**: The render graph can automatically merge compatible passes and cull unused resources
2. **Better Memory Management**: Transient resources are managed efficiently
3. **Clearer Dependencies**: Explicit declaration of texture reads/writes helps the system understand data flow
4. **Native Render Passes**: On mobile platforms, the render graph can automatically use native render passes for better performance
5. **Future Proof**: Aligns with Unity's direction for URP development

## Compatibility Notes

- This implementation is designed for Unity 6000.2 with URP
- The Render Graph is the default path; Compatibility Mode is not supported by this implementation
- Works with both Forward and 2D renderers that support render features

## Testing Recommendations

1. Test with various texture scales to ensure RTHandle reallocation works correctly
2. Verify velocity simulation works as expected with moving emitters
3. Check that layer mask and rendering layer mask filtering work correctly
4. Test the preview feature in the editor
5. Use the Render Graph Viewer (Window > Analysis > Render Graph Viewer) to inspect the graph
6. Check for performance improvements compared to the legacy implementation

## Shader Compatibility

The existing shaders (Blit.shader and Emitter.shader) remain compatible with Unity 6000.2:
- They use standard URP shader library includes
- The HLSL code is up-to-date with Unity 6 conventions
- No shader changes were required for this migration

## Migration Path

If you need to support older Unity versions, you would need to:
1. Keep both the old `Execute()` method and new `RecordRenderGraph()` method
2. Use `#if` directives to compile the appropriate code based on Unity version
3. However, this implementation assumes Unity 6000.2+ only

## Future Improvements

Potential optimizations for future consideration:
1. Investigate pass merging opportunities with other render features
2. Explore compute shader usage for velocity simulation
3. Consider using temporary textures for intermediate results instead of persistent RTHandles where appropriate
4. Profile and optimize renderer list creation
