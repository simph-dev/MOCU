// The rear-view screen, both views. Draws the screen's image - a car's mirror, a
// parking display - and wherever the image is green, the view behind the car
// instead: each eye the picture rendered for it, in the Screen view the same one for
// both. HDRP Unlit can do none of that: it samples one texture for both eyes and
// has no second one to tell glass from frame - hence this shader.
//
// Three kinds of pixel in _ScreenImage, read through the second set of texture
// coordinates, which run left to right as the participant sees the screen whatever
// the picture's own do:
//
//     alpha below half         not drawn at all: what is behind shows through
//     clearly green            the glass: the view behind the car
//     anything else            the image itself: housing, bezel, frame
//
// How green is green above both red and blue, in linear colour. From GlassTo up
// it is all glass - pure green, lime, salad; up to GlassFrom all frame - a dark or
// grey frame, the odd greenish tint in a photograph. In between it is both, mixed
// in proportion: the texture's filtering, and further off its smaller mipmaps,
// blend glass and frame along the edge of the glass, and those pixels show a share
// of each instead of a green haze. The frame's colour has the green taken back out
// of it for the same reason, so nothing of the frame is left greener than it is
// red or blue.
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
Shader "MirrorExperiment/RearViewScreen"
{
    Properties
    {
        _LeftEyeTexture("Left eye", 2D) = "black" {}
        _RightEyeTexture("Right eye", 2D) = "black" {}
        _ScreenImage("Screen image (green = glass)", 2D) = "white" {}
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

            #define GlassFrom 0.1
            #define GlassTo 0.7

            TEXTURE2D(_LeftEyeTexture);
            SAMPLER(sampler_LeftEyeTexture);
            TEXTURE2D(_RightEyeTexture);
            SAMPLER(sampler_RightEyeTexture);

            // Clamped whatever the image was imported with: repeating, its edges
            // would bleed in from the opposite side.
            TEXTURE2D(_ScreenImage);
            SAMPLER(sampler_linear_clamp_image);

            struct Attributes
            {
                float3 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                float2 imageUV    : TEXCOORD1;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                float2 imageUV    : TEXCOORD1;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                output.positionCS = TransformWorldToHClip(TransformObjectToWorld(input.positionOS));
                output.uv = input.uv;
                output.imageUV = input.imageUV;
                return output;
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float4 image = SAMPLE_TEXTURE2D(_ScreenImage, sampler_linear_clamp_image, input.imageUV);
                clip(image.a - 0.5);

                float notGreen = max(image.r, image.b);
                float glass = saturate((image.g - notGreen - GlassFrom) / (GlassTo - GlassFrom));
                float3 frame = float3(image.r, min(image.g, notGreen), image.b);

                if (glass <= 0.0)
                    return float4(frame, 1.0);

            #if defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
                uint eye = unity_StereoEyeIndex;
            #else
                uint eye = 0;
            #endif

                float3 behind = eye == 0
                    ? SAMPLE_TEXTURE2D(_LeftEyeTexture, sampler_LeftEyeTexture, input.uv).rgb
                    : SAMPLE_TEXTURE2D(_RightEyeTexture, sampler_RightEyeTexture, input.uv).rgb;

                return float4(lerp(frame, behind, glass), 1.0);
            }

            ENDHLSL
        }
    }
}
