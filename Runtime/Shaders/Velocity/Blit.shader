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

                // Sample the velocity texture
                float4 velocityData = SAMPLE_TEXTURE2D(_PG_PreviousVelocityTexture, sampler_PG_PreviousVelocityTexture, input.uv);

                // Extract velocity (stored in zw channels)
                float2 velocity = velocityData.zw;

                // Also check temporary velocity for current frame emitters
                float2 temporaryVelocity = SAMPLE_TEXTURE2D(_PG_TemporaryVelocityTexture, sampler_PG_TemporaryVelocityTexture, input.uv).zw;
                if (abs(temporaryVelocity.x) > 0)
                    velocity.x = temporaryVelocity.x;
                if (abs(temporaryVelocity.y) > 0)
                    velocity.y = temporaryVelocity.y;

                // Visualize velocity: map from [-range, +range] to [0, 1] for RGB display
                // Velocity is multiplied by 6 in the emitter, so we divide to normalize
                float velocityScale = 6.0;
                float2 normalizedVelocity = velocity / velocityScale;

                // Map [-1, 1] to [0, 1] for visualization: v * 0.5 + 0.5
                float2 visualVelocity = normalizedVelocity * 0.5 + 0.5;

                // Show velocity as color: X = Red, Y = Green, magnitude = Blue
                float magnitude = length(normalizedVelocity);

                return float4(visualVelocity.x, visualVelocity.y, magnitude, 1.0);
            }
            ENDHLSL
        }
    }
}
