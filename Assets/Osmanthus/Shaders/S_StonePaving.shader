Shader "Osmanthus/StonePaving"
{
    // Procedural stone paving in world space, for the corridor and pavilion floor.
    //
    // The scene already contains 55 joint/grout strips, but they sit at y 0.00-0.02 underneath the
    // floor inset at y 0.18-0.21, so none of them has ever been visible. Rather than lift and refit
    // that geometry, the slabs are drawn here: it follows the floor wherever it goes and costs one
    // material.
    //
    // Unlit on purpose. The key light is a strong orange (1.0, 0.72, 0.43) over a dark teal ambient,
    // which turns the near-white stone of M_Stone_Edge to mud; picking the tones directly avoids it.
    Properties
    {
        _StoneA ("Stone Dark", Color) = (0.44,0.43,0.40,1)
        _StoneB ("Stone Light", Color) = (0.60,0.58,0.53,1)
        _GroutColor ("Grout", Color) = (0.26,0.25,0.23,1)
        _SlabX ("Slab Size X", Float) = 0.62
        _SlabZ ("Slab Size Z", Float) = 0.92
        _GroutWidth ("Grout Width", Range(0.005,0.12)) = 0.035
        _RunningBond ("Running Bond", Range(0,1)) = 1
        _SlabVariation ("Per-Slab Variation", Range(0,1)) = 0.55
        _GrainStrength ("Grain", Range(0,1)) = 0.10
        _GrainScale ("Grain Scale", Float) = 14
        _WearStrength ("Centre Wear", Range(0,1)) = 0.18
        _DetailFade ("Detail Fade Distance", Float) = 26
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _StoneA;
                float4 _StoneB;
                float4 _GroutColor;
                float _SlabX;
                float _SlabZ;
                float _GroutWidth;
                float _RunningBond;
                float _SlabVariation;
                float _GrainStrength;
                float _GrainScale;
                float _WearStrength;
                float _DetailFade;
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
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                float3 wp = IN.positionWS;
                float dist = distance(wp, _WorldSpaceCameraPos);
                // Grout is a hard, high-frequency feature; letting it run to the horizon aliases
                // badly at grazing angles in a headset, so it dissolves out with distance.
                float crisp = 1.0 - smoothstep(_DetailFade * 0.45, _DetailFade, dist);

                // Rows run along Z; alternate rows shift half a slab for a running bond.
                float row = floor(wp.z / _SlabZ);
                float offset = _RunningBond * 0.5 * _SlabX * fmod(abs(row), 2.0);
                float2 cell = float2(floor((wp.x + offset) / _SlabX), row);
                float2 f = float2(frac((wp.x + offset) / _SlabX), frac(wp.z / _SlabZ));

                // Distance to the nearest slab edge, in metres, then a soft grout band.
                float edgeX = min(f.x, 1.0 - f.x) * _SlabX;
                float edgeZ = min(f.y, 1.0 - f.y) * _SlabZ;
                float edge = min(edgeX, edgeZ);
                float grout = 1.0 - smoothstep(_GroutWidth * 0.45, _GroutWidth, edge);
                grout *= crisp;

                // Each slab gets its own tone so the floor is not one flat sheet.
                float slabTone = Hash21(cell + 3.17);
                slabTone = lerp(0.5, slabTone, _SlabVariation);
                half3 col = lerp(_StoneA.rgb, _StoneB.rgb, slabTone);

                // Fine grain inside the slabs, plus a little polish along the walked centre line.
                float grain = ValueNoise(wp.xz * _GrainScale) - 0.5;
                col *= 1.0 + grain * _GrainStrength * crisp;
                float wear = 1.0 - smoothstep(0.0, 1.1, abs(wp.x));
                col *= 1.0 + wear * _WearStrength;

                col = lerp(col, _GroutColor.rgb, grout);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
