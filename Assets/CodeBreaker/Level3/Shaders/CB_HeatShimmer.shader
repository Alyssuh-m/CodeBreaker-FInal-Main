// Code Breaker - Level 3: subtle heat shimmer for the hot aisles behind the server racks.
// Bends the already-rendered scene (URP opaque texture) a little; fades out at the quad's edges.
Shader "CodeBreaker/Heat Shimmer"
{
    Properties
    {
        _Strength ("Distortion strength", Range(0, 0.03)) = 0.006
        _Speed ("Speed", Float) = 0.7
        _Scale ("Wave scale", Float) = 5
        _Tint ("Warm tint (A = amount)", Color) = (1, 0.62, 0.32, 0.06)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Shimmer"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareOpaqueTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Strength;
                float _Speed;
                float _Scale;
                float4 _Tint;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float4 screenPos : TEXCOORD1; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float2 suv = i.screenPos.xy / i.screenPos.w;
                float t = _Time.y * _Speed;
                float edge = smoothstep(0.0, 0.2, i.uv.x) * smoothstep(1.0, 0.8, i.uv.x)
                           * smoothstep(0.0, 0.12, i.uv.y) * smoothstep(1.0, 0.55, i.uv.y);
                float2 wave = float2(
                    sin(i.uv.y * _Scale * 6.2832 - t * 3.1 + sin(i.uv.x * 9.0 + t)),
                    cos(i.uv.y * _Scale * 4.3 - t * 2.4 + i.uv.x * 7.0));
                float2 offset = wave * _Strength * edge;
                half3 col = SampleSceneColor(suv + offset);
                col = lerp(col, col * _Tint.rgb * 1.6, _Tint.a * edge);
                return half4(col, edge);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
