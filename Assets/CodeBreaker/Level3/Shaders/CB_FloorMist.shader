// Code Breaker - Level 3: low cold-aisle mist. Soft noise that drifts slowly; vertex alpha fades the
// sheet out at its borders so there are no hard edges. Transparent, unlit, fog-aware, very cheap.
Shader "CodeBreaker/Floor Mist"
{
    Properties
    {
        _BaseMap ("Noise (A)", 2D) = "white" {}
        _BaseColor ("Colour (A = density)", Color) = (0.62, 0.78, 1, 0.16)
        _Scroll ("Drift speed (xy)", Vector) = (0.012, 0.005, 0, 0)
        _Tiling ("Tiling (xy)", Vector) = (1.6, 0.9, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass
        {
            Name "Mist"
            Tags { "LightMode" = "UniversalForward" }
            ZWrite Off
            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _Scroll;
                float4 _Tiling;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float alpha : TEXCOORD1; float fog : TEXCOORD2; float3 positionWS : TEXCOORD3; };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = i.uv;
                o.alpha = i.color.a;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                // two layers drifting at different speeds so the pattern never visibly repeats
                float2 uvA = i.uv * _Tiling.xy + _Time.y * _Scroll.xy + i.positionWS.xz * 0.013;
                float2 uvB = i.uv * _Tiling.xy * 0.63 - _Time.y * _Scroll.yx * 0.7 + 0.37;
                half n = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvA).a * 0.6 + SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uvB).a * 0.6;
                half a = saturate(n) * i.alpha * _BaseColor.a;
                half3 col = MixFog(_BaseColor.rgb, i.fog);
                return half4(col, a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
