// The rear-view mirror's screen in the Mirror view: each eye sees the picture
// rendered for that eye. HDRP Unlit cannot do that - it samples one texture for
// both eyes - hence this shader.
//
// Single-pass instanced stereo comes from multi_compile_instancing and the stereo
// macros, as in HDRP's own shaders. Without a headset the eye index is 0 and the
// left picture shows.
//
// Like HDRP Unlit, the colour is written as it is: exposure does not touch it, and
// the headset camera's tonemapping reaches it once, as it reaches the stars seen
// directly.
//
// In Resources so that a build includes it, since nothing in the scene uses it.
Shader "MirrorExperiment/StereoMirrorScreen"
{
    Properties
    {
        _LeftEyeTexture("Left eye", 2D) = "black" {}
        _RightEyeTexture("Right eye", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderPipeline" = "HDRenderPipeline" "RenderType" = "Opaque" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardOnly"
            Tags { "LightMode" = "ForwardOnly" }

            Cull Off
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM

            #pragma target 4.5
            #pragma only_renderers d3d11 playstation xboxone xboxseries vulkan metal switch switch2
            #pragma multi_compile_instancing

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.high-definition/Runtime/ShaderLibrary/ShaderVariables.hlsl"

            TEXTURE2D(_LeftEyeTexture);
            SAMPLER(sampler_LeftEyeTexture);
            TEXTURE2D(_RightEyeTexture);
            SAMPLER(sampler_RightEyeTexture);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                output.uv = input.uv;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

            #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                uint eye = unity_StereoEyeIndex;
            #else
                uint eye = 0;
            #endif

                float3 color = eye == 0
                    ? SAMPLE_TEXTURE2D(_LeftEyeTexture, sampler_LeftEyeTexture, input.uv).rgb
                    : SAMPLE_TEXTURE2D(_RightEyeTexture, sampler_RightEyeTexture, input.uv).rgb;

                return float4(color, 1.0);
            }

            ENDHLSL
        }
    }
}
