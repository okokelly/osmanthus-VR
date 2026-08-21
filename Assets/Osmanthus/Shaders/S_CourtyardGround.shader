Shader "Osmanthus/CourtyardGround"
{
    // Unlit ground for the courtyard: grass, gravel path and shore bank all use this, differing only
    // by their colour pair. Unlit to match the rest of the 2.5D dressing -- the scene's key light is
    // a strong warm directional, and putting a large neutral Lit plane under it turns it to mud (the
    // same thing that happened to the stone terrace).
    //
    // Mottling is two octaves of value noise. Both fade out with camera distance: a large ground
    // plane seen at a grazing angle will shimmer badly in a headset if high-frequency detail is left
    // running to the horizon.
    Properties
    {
        _ColorA ("Base A", Color) = (0.24,0.30,0.16,1)
        _ColorB ("Base B", Color) = (0.34,0.40,0.21,1)
        _PatchScale ("Patch Scale", Float) = 0.06
        _PatchContrast ("Patch Contrast", Range(0.2,4)) = 1.4
        _MidScale ("Mid Scale", Float) = 0.35
        _MidStrength ("Mid Strength", Range(0,1)) = 0.22
        _MidFade ("Mid Fade Distance", Float) = 65
        _DetailScale ("Detail Scale", Float) = 3.0
        _DetailStrength ("Detail Strength", Range(0,1)) = 0.16
        _DetailFade ("Detail Fade Distance", Float) = 18
        _PatchFade ("Patch Fade Distance", Float) = 130
        _HazeColor ("Distance Haze", Color) = (0.42,0.44,0.40,1)
        _HazeStart ("Haze Start", Float) = 45
        _HazeEnd ("Haze End", Float) = 210
        _HazeMax ("Haze Max", Range(0,1)) = 0.9
        _EdgeFadeStart ("Edge Fade Start", Float) = 0
        _EdgeFadeEnd ("Edge Fade End", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                float4 _ColorA;
                float4 _ColorB;
                float _PatchScale;
                float _PatchContrast;
                float _MidScale;
                float _MidStrength;
                float _MidFade;
                float _DetailScale;
                float _DetailStrength;
                float _DetailFade;
                float _PatchFade;
                float4 _HazeColor;
                float _HazeStart;
                float _HazeEnd;
                float _HazeMax;
                float _EdgeFadeStart;
                float _EdgeFadeEnd;
            CBUFFER_END

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float ValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                float a = Hash21(i);
                float b = Hash21(i + float2(1, 0));
                float c = Hash21(i + float2(0, 1));
                float d = Hash21(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.positionWS = pos.positionWS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 wp = IN.positionWS;
                float d = distance(wp, _WorldSpaceCameraPos);

                float patchFade = 1.0 - smoothstep(_PatchFade * 0.4, _PatchFade, d);
                float detailFade = 1.0 - smoothstep(_DetailFade * 0.4, _DetailFade, d);

                // Large drifting patches of two grass tones.
                float patch = ValueNoise(wp.xz * _PatchScale);
                patch = saturate((patch - 0.5) * _PatchContrast + 0.5);
                patch = lerp(0.5, patch, patchFade);
                half3 col = lerp(_ColorA.rgb, _ColorB.rgb, patch);

                // Mid octave: the clumping you actually read as ground texture while walking. The
                // large patches are too broad to see from inside the corridor and the fine grain is
                // gone by 18 m, so without this the lawn is a flat sheet at eye level.
                float midFade = 1.0 - smoothstep(_MidFade * 0.4, _MidFade, d);
                float mid = ValueNoise(wp.xz * _MidScale + 17.3) - 0.5;
                col *= 1.0 + mid * _MidStrength * midFade;

                // Fine tufting, killed off quickly so it never shimmers at distance.
                float detail = ValueNoise(wp.xz * _DetailScale) - 0.5;
                col *= 1.0 + detail * _DetailStrength * detailFade;

                // Haze into the painted backdrop so the ground plane has no visible end.
                float haze = smoothstep(_HazeStart, _HazeEnd, d) * _HazeMax;
                col = lerp(col, _HazeColor.rgb, haze);

                // Optional soft alpha edge, used by the gravel paths to avoid hard cut ends.
                float alpha = _ColorA.a;
                if (_EdgeFadeEnd > _EdgeFadeStart)
                {
                    float edge = min(IN.uv.y, 1.0 - IN.uv.y) * 2.0; // 0 at the ribbon edges, 1 at centre
                    alpha *= smoothstep(0.0, 0.35, edge);
                }
                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
