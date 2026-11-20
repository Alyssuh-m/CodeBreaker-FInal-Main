// Code Breaker - Level 3: a glass raised-floor tile that shows the cable space underneath.
// The floor itself is not cut: the depth is faked with parallax (the deeper layer shifts with the
// view angle, and the walls of the cavity appear at the edges). Underfloor LEDs blink at different
// rates; a cracked tile can pulse red (_RedOn, driven by L3FlickerLight).
Shader "CodeBreaker/Underfloor Window"
{
    Properties
    {
        _MainTex ("Underfloor", 2D) = "black" {}
        _LedMask ("LED mask (R led, G phase, B amber)", 2D) = "black" {}
        _Crack ("Crack (A)", 2D) = "black" {}
        _Depth ("Cavity depth (world units)", Float) = 1.4
        _TileSize ("Tile size (world units)", Float) = 2.25
        _Brightness ("Underfloor brightness", Float) = 1.0
        _LedGreen ("LED colour", Color) = (0.25, 1, 0.45, 1)
        _LedAmber ("LED colour 2", Color) = (1, 0.65, 0.15, 1)
        _LedIntensity ("LED intensity", Float) = 2.5
        _Frame ("Frame width (0-0.2)", Range(0, 0.2)) = 0.045
        _FrameColor ("Frame colour", Color) = (0.32, 0.33, 0.36, 1)
        _GlassTint ("Glass tint", Color) = (0.75, 0.85, 0.95, 1)
        _CrackAmount ("Crack amount", Range(0, 1)) = 0
        _RedColor ("Red light colour", Color) = (1, 0.08, 0.04, 1)
        _RedOn ("Red light (0-1)", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Window"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_LedMask); SAMPLER(sampler_LedMask);
            TEXTURE2D(_Crack);   SAMPLER(sampler_Crack);

            CBUFFER_START(UnityPerMaterial)
                float _Depth, _TileSize, _Brightness, _LedIntensity, _Frame, _CrackAmount, _RedOn;
                float4 _LedGreen, _LedAmber, _FrameColor, _GlassTint, _RedColor;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };

            Varyings vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.uv = i.uv;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float3 V = normalize(GetCameraPositionWS() - i.positionWS);   // toward the camera
                float vy = max(V.y, 0.18);
                // The tile lies flat with uv.x along world +X and uv.y along world +Z.
                float2 shift = -V.xz / vy * (_Depth / _TileSize);
                float2 deep = i.uv + shift;

                half3 col;
                float inside = step(0.0, deep.x) * step(deep.x, 1.0) * step(0.0, deep.y) * step(deep.y, 1.0);
                half3 floorCol = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, saturate(deep)).rgb * _Brightness;
                half4 led = SAMPLE_TEXTURE2D(_LedMask, sampler_LedMask, saturate(deep));
                float blink = step(0.45, frac(_Time.y * (0.35 + led.g * 1.4) + led.g * 7.0));
                half3 ledCol = lerp(_LedGreen.rgb, _LedAmber.rgb, led.b) * led.r * blink * _LedIntensity;

                // where the deep ray leaves the tile we see the cavity wall: dark, getting darker with depth
                float2 outside = max(0.0, max(-deep, deep - 1.0));
                float wallShade = 0.06 * saturate(1.0 - length(outside) * 3.0);
                col = lerp(half3(wallShade, wallShade, wallShade * 1.1), floorCol + ledCol, inside);

                // red glow from below a cracked tile
                col += _RedColor.rgb * _RedOn * (0.06 + 0.22 * inside) * (0.6 + 0.4 * floorCol.r);

                // glass: tint and a fresnel sheen
                float fres = pow(1.0 - saturate(V.y), 4.0);
                col = col * _GlassTint.rgb + fres * 0.12;

                // cracks sit on the glass surface (no parallax)
                half crack = SAMPLE_TEXTURE2D(_Crack, sampler_Crack, i.uv).a * _CrackAmount;
                col = lerp(col, half3(0.62, 0.65, 0.68) + _RedColor.rgb * _RedOn * 0.25, crack * 0.75);

                // aluminium frame around the glass
                float2 e = min(i.uv, 1.0 - i.uv);
                float frame = 1.0 - step(_Frame, min(e.x, e.y));
                col = lerp(col, _FrameColor.rgb, frame);

                col = MixFog(col, i.fog);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
