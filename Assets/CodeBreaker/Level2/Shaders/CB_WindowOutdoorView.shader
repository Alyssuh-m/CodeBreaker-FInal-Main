// Code Breaker: the view through a window. Looks up a 360-degree garden panorama in the direction
// from the camera through the glass, so the outside keeps the right perspective from every angle.
Shader "CodeBreaker/Window Outdoor View"
{
    Properties
    {
        [NoScaleOffset] _Pano ("Panorama (equirectangular)", 2D) = "white" {}
        _Exposure ("Exposure", Range(0, 2)) = 0.9
        _Tint ("Glass Tint", Color) = (0.97, 0.99, 1, 1)
        _Rotation ("Rotation (degrees)", Range(0, 360)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_Pano); SAMPLER(sampler_Pano);
            CBUFFER_START(UnityPerMaterial)
                half _Exposure;
                half4 _Tint;
                float _Rotation;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(o.positionWS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.positionWS - _WorldSpaceCameraPos);
                float s, c;
                sincos(radians(_Rotation), s, c);
                d = float3(c * d.x - s * d.z, d.y, s * d.x + c * d.z);
                float2 uv = float2(atan2(d.x, d.z) / (2.0 * PI) + 0.5, asin(clamp(d.y, -1.0, 1.0)) / PI + 0.5);
                half3 col = SAMPLE_TEXTURE2D_LOD(_Pano, sampler_Pano, uv, 0).rgb;
                return half4(col * _Exposure * _Tint.rgb, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            float4 vert(Attributes i) : SV_POSITION { return TransformObjectToHClip(i.positionOS.xyz); }
            half frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert(Attributes i) { Varyings o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(i.normalOS); return o; }
            half4 frag(Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
    FallBack Off
}
