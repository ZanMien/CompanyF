// Inverted-hull outline: draws back faces pushed out along the normals by a fixed number of screen pixels.
Shader "CompanyF/Outline"
{
    Properties
    {
        _OutlineColor ("Color", Color) = (0, 0, 0, 1)
        _OutlineWidth ("Width (pixels)", Range(0, 20)) = 5
        _Rainbow ("Rainbow (0 = solid Color)", Range(0, 1)) = 1
        _RainbowScale ("Rainbow bands per metre", Float) = 0.5
        _RainbowSpeed ("Rainbow cycles per second", Float) = 0.6
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
                float _Rainbow;
                float _RainbowScale;
                float _RainbowSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalCS = TransformWorldToHClipDir(TransformObjectToWorldNormal(input.normalOS));
                float2 direction = normalize(normalCS.xy + float2(1e-5, 1e-5));
                positionCS.xy += direction * _OutlineWidth * 2.0 / _ScreenParams.xy * positionCS.w;
                output.positionCS = positionCS;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float hue = dot(input.positionWS, float3(1, 1, 1)) * _RainbowScale - _Time.y * _RainbowSpeed;
                half3 rainbow = saturate(abs(frac(hue + float3(1.0, 2.0 / 3.0, 1.0 / 3.0)) * 6.0 - 3.0) - 1.0);
                return half4(lerp(_OutlineColor.rgb, rainbow, _Rainbow), 1);
            }
            ENDHLSL
        }
    }
}
