// Inverted-hull outline: draws back faces pushed out along the normals by a fixed number of screen pixels.
Shader "CompanyF/Outline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Width (pixels)", Range(0, 20)) = 4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front
            ZWrite On

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _OutlineColor;
                float _OutlineWidth;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            float4 Vert(Attributes input) : SV_POSITION
            {
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalCS = TransformWorldToHClipDir(TransformObjectToWorldNormal(input.normalOS));
                float2 direction = normalize(normalCS.xy + float2(1e-5, 1e-5));
                positionCS.xy += direction * _OutlineWidth * 2.0 / _ScreenParams.xy * positionCS.w;
                return positionCS;
            }

            half4 Frag() : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
