// Code Breaker - Level 3: animated CCTV static / interference for decorative monitors (not puzzle feeds).
Shader "CodeBreaker/Static Noise"
{
    Properties
    {
        _Tint ("Tint", Color) = (0.55, 0.75, 1, 1)
        _Brightness ("Brightness", Float) = 0.9
        _Rolling ("Rolling bar strength", Range(0, 1)) = 0.35
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Static"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Tint; float _Brightness; float _Rolling;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float fog : TEXCOORD1; };
            V vert(A i) { V o; o.positionCS = TransformObjectToHClip(i.positionOS.xyz); o.uv = i.uv; o.fog = ComputeFogFactor(o.positionCS.z); return o; }
            float hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            half4 frag(V i) : SV_Target
            {
                float t = floor(_Time.y * 24.0);
                float2 cell = floor(i.uv * float2(160.0, 120.0));
                float n = hash(cell + t * 17.0);
                float scan = 0.85 + 0.15 * sin(i.uv.y * 600.0);
                float bar = 1.0 + _Rolling * smoothstep(0.08, 0.0, abs(frac(i.uv.y - _Time.y * 0.18) - 0.5));
                float2 e = min(i.uv, 1.0 - i.uv);
                float vign = saturate(min(e.x, e.y) * 8.0);
                half3 c = _Tint.rgb * n * scan * bar * _Brightness * (0.35 + 0.65 * vign);
                c = MixFog(c, i.fog);
                return half4(c, 1);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
