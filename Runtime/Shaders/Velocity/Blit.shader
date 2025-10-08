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
            #include "Packages/com.aarthificial.pixelgraphics/Runtime/Shaders/VelocitySimulation.hlsl"

            float4 Fragment(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 result = SimulateVelocity(input.uv);

                float2 temporaryVelocity = SAMPLE_TEXTURE2D(_PG_TemporaryVelocityTexture, sampler_PG_TemporaryVelocityTexture, input.uv).zw;
                if (abs(temporaryVelocity.x) > 0)
                    result.z = temporaryVelocity.x;
                if (abs(temporaryVelocity.y) > 0)
                    result.w = temporaryVelocity.y;

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
