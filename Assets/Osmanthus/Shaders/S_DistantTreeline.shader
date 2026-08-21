Shader "Osmanthus/DistantTreeline"
{
    // A hazy band of distant planting wrapped right around the courtyard, to give the garden an edge.
    //
    // The painted far-city backdrop only spans 60..300 degrees, centred on the lake, so looking east
    // from the corridor there is nothing at all between the ground plane and the clear colour. This
    // closes that gap with a soft noise silhouette rather than a hard wall, so it does not pre-empt
    // whatever rockery / planting gets placed later.
    Properties
    {
        _NearColor ("Canopy Base", Color) = (0.20,0.24,0.18,1)
        _FarColor ("Canopy Top", Color) = (0.34,0.36,0.32,1)
        _HazeColor ("Haze", Color) = (0.42,0.44,0.41,1)
        _HazeAmount ("Haze Amount", Range(0,1)) = 0.55
        _Ridge ("Silhouette Height", Range(0.1,1)) = 0.62
        _RidgeVariation ("Silhouette Variation", Range(0,1)) = 0.42
        _RidgeScale ("Silhouette Scale", Float) = 26
        _Softness ("Edge Softness", Range(0.002,0.2)) = 0.035
        _BaseFade ("Base Fade", Range(0,0.5)) = 0.12
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _NearColor;
                float4 _FarColor;
                float4 _HazeColor;
                float _HazeAmount;
                float _Ridge;
                float _RidgeVariation;
                float _RidgeScale;
                float _Softness;
                float _BaseFade;
            CBUFFER_END

            float Hash11(float p)
            {
                p = frac(p * 0.1031);
                p *= p + 33.33;
                return frac(p * (p + p));
            }

            float Noise11(float x)
            {
                float i = floor(x);
                float f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(Hash11(i), Hash11(i + 1.0), f);
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = GetVertexPositionInputs(IN.positionOS.xyz).positionCS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                // Three octaves so the canopy line has both big clumps and small breaks.
                float u = IN.uv.x * _RidgeScale;
                float n = Noise11(u) * 0.55 + Noise11(u * 2.7 + 11.3) * 0.30 + Noise11(u * 6.1 + 41.7) * 0.15;
                float ridge = _Ridge * (1.0 + (n - 0.5) * 2.0 * _RidgeVariation);

                float alpha = 1.0 - smoothstep(ridge - _Softness, ridge + _Softness, IN.uv.y);
                // Soften where it meets the ground so the band does not read as a cut-out strip.
                alpha *= smoothstep(0.0, max(_BaseFade, 1e-4), IN.uv.y);

                float h = saturate(IN.uv.y / max(ridge, 1e-4));
                half3 col = lerp(_NearColor.rgb, _FarColor.rgb, h);
                col = lerp(col, _HazeColor.rgb, _HazeAmount);

                return half4(col, alpha);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
