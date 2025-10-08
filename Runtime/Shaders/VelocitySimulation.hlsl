#ifndef VELOCITYSIMULATION_INCLUDE
#define VELOCITYSIMULATION_INCLUDE
#define SAMPLE_PREVIOUS_VELOCITY(coordX, coordY) SAMPLE_TEXTURE2D(_PG_PreviousVelocityTexture, sampler_PG_PreviousVelocityTexture, uv - _PG_CameraPositionDelta.xy + float2(coordX, coordY))

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

CBUFFER_START(UnityPerMaterial)
TEXTURE2D(_PG_PreviousVelocityTexture);
SAMPLER(sampler_PG_PreviousVelocityTexture);
float4 _PG_PreviousVelocityTexture_TexelSize;
TEXTURE2D(_PG_TemporaryVelocityTexture);
SAMPLER(sampler_PG_TemporaryVelocityTexture);

float4 _PG_VelocitySimulationParams;
float4 _PG_CameraPositionDelta;
CBUFFER_END

float4 SimulateVelocity(float2 uv)
{
    float2 offset = _PG_PreviousVelocityTexture_TexelSize.xy;

    // Check edges first - zero out velocity at borders to prevent instability
    if (uv.x < offset.x
        || uv.x > 1 - offset.x
        || uv.y < offset.y
        || uv.y > 1 - offset.y
    )
    {
        return float4(0, 0, 0, 0);
    }

    float4 previousData = SAMPLE_PREVIOUS_VELOCITY(0, 0);

    float4 neighbouringData = SAMPLE_PREVIOUS_VELOCITY(offset.x, 0);
    neighbouringData += SAMPLE_PREVIOUS_VELOCITY(-offset.x, 0);
    neighbouringData += SAMPLE_PREVIOUS_VELOCITY(0, offset.y);
    neighbouringData += SAMPLE_PREVIOUS_VELOCITY(0, -offset.y);

    neighbouringData *= 0.25f;
    previousData = lerp(previousData, neighbouringData, _PG_VelocitySimulationParams.z);

    float2 distance = previousData.xy;
    float2 velocity = previousData.zw;

    // Add current frame's emitter velocity to the distance
    // This is what creates the displacement that the spring will pull back
    float4 temporaryData = SAMPLE_TEXTURE2D(_PG_TemporaryVelocityTexture, sampler_PG_TemporaryVelocityTexture, uv);
    float2 emitterVelocity = temporaryData.zw;

    // Only add emitter velocity if it's significant (avoid accumulating tiny floating point errors)
    if (abs(emitterVelocity.x) > 0.0001 || abs(emitterVelocity.y) > 0.0001)
    {
        distance += emitterVelocity;
        velocity = emitterVelocity; // Set velocity directly for simple decay mode
    }

    // SIMPLE EXPONENTIAL DECAY (for testing)
    // Comment this block and uncomment the spring physics block below to switch
    float decayRate = 0.95; // 0.95 = keep 95% each frame (adjust between 0.8-0.98)
    velocity *= decayRate;
    distance *= decayRate;

    /* SPRING PHYSICS (original - currently disabled for testing)
    float dt = min(unity_DeltaTime.x, _PG_VelocitySimulationParams.w);
    float2 acceleration = -distance * _PG_VelocitySimulationParams.x - velocity * _PG_VelocitySimulationParams.y;
    velocity += acceleration * dt;
    distance += velocity * dt;
    */

    // Clamp extremely small values to exactly zero to prevent floating point drift
    if (abs(distance.x) < 0.0001) distance.x = 0;
    if (abs(distance.y) < 0.0001) distance.y = 0;
    if (abs(velocity.x) < 0.0001) velocity.x = 0;
    if (abs(velocity.y) < 0.0001) velocity.y = 0;

    return float4(distance.x, distance.y, velocity.x, velocity.y);
}

#endif //VELOCITYSIMULATION_INCLUDE
