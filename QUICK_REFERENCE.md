# Render Graph Quick Reference

## Before (Legacy API)
```csharp
public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
{
    var cmd = CommandBufferPool.Get();

    // Get temporary render textures
    cmd.GetTemporaryRT(id, width, height, 0, FilterMode.Bilinear, format);

    // Set global textures
    cmd.SetGlobalTexture(ShaderIds.SomeTexture, id);

    // Render
    cmd.SetRenderTarget(targetId);
    cmd.DrawMesh(...);

    // Draw renderers
    context.DrawRenderers(cullResults, ref drawingSettings, ref filteringSettings);

    // Cleanup
    cmd.ReleaseTemporaryRT(id);
    context.ExecuteCommandBuffer(cmd);
    CommandBufferPool.Release(cmd);
}
```

## After (Render Graph API)
```csharp
private class PassData
{
    internal TextureHandle inputTexture;
    internal Material material;
    internal RendererListHandle rendererList;
}

private static void ExecutePass(PassData data, RasterGraphContext context)
{
    var cmd = context.cmd;
    // Execute rendering commands
    cmd.DrawMesh(...);
    cmd.DrawRendererList(data.rendererList);
}

public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
{
    var resourceData = frameData.Get<UniversalResourceData>();
    var cameraData = frameData.Get<UniversalCameraData>();
    var renderingData = frameData.Get<UniversalRenderingData>();

    // Create or import textures
    RTHandle myRTHandle = ...;
    TextureHandle myTexture = renderGraph.ImportTexture(myRTHandle);

    // Add a render pass
    using (var builder = renderGraph.AddRasterRenderPass<PassData>("My Pass", out var passData))
    {
        // Populate pass data
        passData.inputTexture = resourceData.activeColorTexture;
        passData.material = myMaterial;

        // Create renderer list
        var rendererListDesc = new RendererListDesc(shaderTagId, cullResults, camera);
        passData.rendererList = renderGraph.CreateRendererList(rendererListDesc);

        // Declare dependencies
        builder.UseTexture(passData.inputTexture, AccessFlags.Read);
        builder.UseRendererList(passData.rendererList);
        builder.SetRenderAttachment(myTexture, 0, AccessFlags.Write);

        // Set global texture
        builder.SetGlobalTextureAfterPass(myTexture, ShaderIds.SomeTexture);

        // Set render function
        builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));
    }
}
```

## Key Differences

| Legacy API | Render Graph API |
|------------|------------------|
| `cmd.GetTemporaryRT()` | `renderGraph.CreateTexture()` or `renderGraph.ImportTexture()` |
| `cmd.SetGlobalTexture()` | `builder.SetGlobalTextureAfterPass()` |
| `cmd.SetRenderTarget()` | `builder.SetRenderAttachment()` |
| `context.DrawRenderers()` | `cmd.DrawRendererList()` with `RendererListHandle` |
| `Execute()` method | `RecordRenderGraph()` method |
| Direct command buffer execution | Static render function with `PassData` |
| Manual resource management | Automatic resource management |

## Common Patterns

### Creating a Persistent RTHandle
```csharp
// In your pass class
private RTHandle _myTexture;

// In RecordRenderGraph
RenderingUtils.ReAllocateHandleIfNeeded(ref _myTexture, descriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "MyTexture");
TextureHandle handle = renderGraph.ImportTexture(_myTexture);
```

### Creating a Temporary Texture
```csharp
var desc = new TextureDesc(width, height)
{
    colorFormat = GraphicsFormat.R16G16B16A16_SFloat,
    name = "TempTexture"
};
TextureHandle tempTexture = renderGraph.CreateTexture(desc);
```

### Using Textures from UniversalResourceData
```csharp
var resourceData = frameData.Get<UniversalResourceData>();
TextureHandle colorTexture = resourceData.activeColorTexture;
TextureHandle depthTexture = resourceData.activeDepthTexture;
```

### Blit Operation
```csharp
var blitParams = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, pass);
renderGraph.AddBlitPass(blitParams, passName: "Blit");
```

### Setting Multiple Render Attachments (MRT)
```csharp
builder.SetRenderAttachment(texture1, 0, AccessFlags.Write);
builder.SetRenderAttachment(texture2, 1, AccessFlags.Write);
builder.SetRenderAttachment(texture3, 2, AccessFlags.Write);
```

### Accessing Camera Data
```csharp
var cameraData = frameData.Get<UniversalCameraData>();
var viewMatrix = cameraData.GetViewMatrix();
var projectionMatrix = cameraData.GetProjectionMatrix();
var camera = cameraData.camera;
```

## Debugging Tips

1. **Use Render Graph Viewer**: Window > Analysis > Render Graph Viewer
2. **Disable Pass Culling**: `builder.AllowPassCulling(false)` to prevent automatic removal
3. **Check Handle Validity**: `if (handle.IsValid()) { ... }`
4. **Profile with Frame Debugger**: Check pass execution order and resource usage

## Important Notes

- TextureHandles are only valid for the current frame
- PassData should only contain data needed by the render function
- Render functions should be static to avoid capturing unwanted state
- Use `builder.UseTexture()` for all texture reads, even if they're global
- RTHandles should be disposed in a `Dispose()` method
