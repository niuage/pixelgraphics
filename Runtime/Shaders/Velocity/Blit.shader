Shader "Hidden/PixelGraphics/Velocity/Blit"
{
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline"
        }
        LOD 100

        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION
            #pragma multi_compile _ _USE_DRAW_PROCEDURAL

            #include "Packages/com.aarthificial.pixelgraphics/Runtime/Shaders/Fullscreen.hlsl"
            #include "Packages/com.aarthificial.pixelgraphics/Runtime/Shaders/VelocitySimulation.hlsl"

            float4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                return SimulateVelocity(input.uv);
            }
            ENDHLSL
        }
        Pass
        {
            ZTest Always
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex FullscreenVert
            #pragma fragment Fragment
            #pragma multi_compile_fragment _ _LINEAR_TO_SRGB_CONVERSION
            #pragma multi_compile _ _USE_DRAW_PROCEDURAL

            #include "Packages/com.aarthificial.pixelgraphics/Runtime/Shaders/Fullscreen.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_PG_VelocityTexture);
            SAMPLER(sampler_PG_VelocityTexture);

            // Add camera delta for debugging
            float4 _PG_CameraPositionDelta;

            float4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                // Read the FINAL simulated velocity texture (not re-simulate!)
                float4 result = SAMPLE_TEXTURE2D(_PG_VelocityTexture, sampler_PG_VelocityTexture, input.uv);

                // DEBUG: Show camera delta in top-left corner
                if (input.uv.x < 0.2 && input.uv.y > 0.8)
                {
                    // Visualize camera delta magnitude
                    float deltaMag = length(_PG_CameraPositionDelta.xy) * 100.0; // Scale up for visibility
                    return float4(deltaMag, deltaMag, 0, 1);
                }

                // Preview visualization: remap velocity from [-1, 1] to [0, 1] for display
                // Velocity is stored in zw components (xy is distance)
                float2 velocity = result.zw;

                // Remap from [-1, 1] to [0, 1]
                // velocity * 0.5 + 0.5 maps: -1 -> 0, 0 -> 0.5, 1 -> 1
                float2 visualVelocity = velocity * 0.5 + 0.5;

                // Use velocity for color visualization
                // Red = horizontal velocity, Green = vertical velocity
                // Middle gray (0.5, 0.5) = no movement
                // Brighter = positive direction, Darker = negative direction
                return float4(visualVelocity.x, visualVelocity.y, 0, 1);
            }
            ENDHLSL
        }
    }
}
