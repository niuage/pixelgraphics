# Velocity Render Feature - Debugging Guide

## Quick Checklist

### 1. **Check Console Logs**
Run your game and look for this log message:
```
[VelocityRenderPass] RecordRenderGraph called - Preview: True/False, LayerMask: X, RenderingLayerMask: Y
```

- If you **DON'T see this log**, the pass is not being called at all
- If you **DO see this log**, the pass is running

### 2. **Verify Renderer Feature Setup**

In your URP 2D Renderer asset:
- [ ] VelocityRenderFeature is added
- [ ] It's enabled (checkbox is ticked)
- [ ] Check the settings:
  - `Pixels Per Unit`: Should match your sprite PPU (default 24)
  - `Texture Scale`: 0.5 is fine for testing
  - `Preview`: Check this box to see the velocity texture

### 3. **Check Layer/Rendering Layer Configuration**

You have TWO options for emitters:

#### Option A: Using Layer Mask (Simpler)
1. **Create a new Layer** (e.g., "VelocityEmitter")
2. **In VelocityRenderFeature settings:**
   - Set `Layer Mask` to include your "VelocityEmitter" layer
   - Leave `Rendering Layer Mask` at 0
3. **On your emitter GameObject:**
   - Set the Layer to "VelocityEmitter"
4. **On your main camera:**
   - EXCLUDE the "VelocityEmitter" layer from the Culling Mask (so it's invisible to the main camera)

#### Option B: Using Rendering Layer Mask
1. **In VelocityRenderFeature settings:**
   - Leave `Layer Mask` at 0
   - Set `Rendering Layer Mask` to a specific value (e.g., "Rendering Layer 1")
2. **On your emitter's Sprite Renderer:**
   - Set the Rendering Layer Mask to match (e.g., "Rendering Layer 1")

### 4. **Verify Emitter Shader**

Check your emitter object's material:
- [ ] The shader should be "Aarthificial/PixelGraphics/Emitter" (or similar)
- [ ] If it's pink, the shader is missing or has errors

### 5. **Enable Frame Debugger**

Open **Window > Analysis > Frame Debugger** and click Enable:

1. Look for passes named:
   - "Velocity Simulation"
   - "Velocity Emitters" (only if you have layer/rendering layer mask set)
   - "Velocity Preview" (only if Preview is checked)

2. Click on "Velocity Simulation":
   - You should see it rendering to `_PG_VelocityTarget_A` or `_PG_VelocityTarget_B`
   - Check if it's rendering anything

3. Click on "Velocity Preview":
   - This should blit the velocity texture to the screen
   - If you see this pass but nothing on screen, the blit shader might be wrong

### 6. **Common Issues**

#### Issue: Nothing appears when Preview is checked
**Possible causes:**
- Preview pass is being culled
- Wrong shader pass index in the blit
- Velocity texture is empty (no emitters rendering)

**Solution:**
Check the Frame Debugger to see if passes are executing.

#### Issue: Pass is being culled
**Possible causes:**
- Render Graph is optimizing away unused passes
- Textures aren't being used by anything downstream

**Solution:**
I've added `builder.AllowPassCulling(false)` to force the simulation pass to run.

#### Issue: Emitters not rendering
**Possible causes:**
- Wrong layer/rendering layer mask configuration
- Shader tag mismatch
- Culling issues

**Solution:**
- Double-check layer mask configuration (see step 3)
- Verify the console log shows the correct mask values

### 7. **Test Configuration**

Try this minimal setup:

1. **Create a test layer:**
   - Add a new layer called "VelocityTest"

2. **Configure Renderer Feature:**
   - Layer Mask: Select "VelocityTest"
   - Rendering Layer Mask: 0 (Nothing)
   - Preview: ✓ Checked
   - Pixels Per Unit: 24

3. **Create test emitter:**
   - Create a new GameObject with SpriteRenderer
   - Set Layer to "VelocityTest"
   - Assign the Emitter shader/material
   - Make sure it has a sprite assigned

4. **Configure Main Camera:**
   - Culling Mask: UNCHECK "VelocityTest" layer

5. **Run the game:**
   - You should see the velocity visualization on screen
   - Move the camera to see the velocity texture update

## Expected Behavior

When working correctly:
- **Without Preview:** The velocity texture is created and updated, but you don't see it (it's used by foliage shaders)
- **With Preview:** You should see a colorful visualization of velocity on screen
  - Red/Green channels show velocity direction
  - Brighter areas = more velocity
  - Should fade over time (simulation decay)

## Next Steps

1. Run the game and check the console for the debug log
2. Report back what you see in the Frame Debugger
3. Let me know the values in the log message

This will help us pinpoint exactly what's not working!
