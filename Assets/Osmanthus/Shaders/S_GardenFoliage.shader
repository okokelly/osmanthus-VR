Shader "Osmanthus/GardenFoliage"
{
    // Foliage lighting for the seven garden assets.
    //
    // The plants are built from single-sided leaf cards. Under URP Lit with this scene's single
    // orange key light, roughly half the leaves on every tree face away from the sun, fall through
    // to the dark teal ambient, and render as black shards - the tree reads as a scribble rather
    // than a canopy. Raising the scene ambient does not fix it: it lifts the stones and the ground
    // long before the leaves catch up, and washes out the dusk.
    //
    // So this does what stylised foliage normally does:
    //   - wrap (half-lambert) lighting, so a leaf turned away from the key still receives light
    //     instead of clamping to zero
    //   - two sided, with the normal flipped on back faces so a card lit from behind reads the same
    //     as one lit from the front
    //   - an explicit ambient colour rather than the scene probe, following the same reasoning as
    //     S_StonePaving: with a strong orange key over a dark teal ambient it is cheaper and far
    //     more predictable to pick the tones than to fight the PBR result
    //
    // Cheaper than URP Lit on Quest as well - one texture fetch, no BRDF, no shadow sampling.
    // The stones (02, 04) are solid volumes with sound normals and stay on URP Lit; only the five
    // plants use this.
    Properties
    {
        _BaseMap ("Albedo", 2D) = "white" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        // 0 = plain lambert (black backs), 1 = fully wrapped (flat, no form).
        _Wrap ("Light Wrap", Range(0,1)) = 0.65
        _AmbientColor ("Ambient", Color) = (0.34,0.38,0.35,1)
        _KeyStrength ("Key Strength", Range(0,2)) = 0.95
        // Canopy interiors read better with a little depth; darkens the underside of the volume.
        _BaseShade ("Underside Shading", Range(0,1)) = 0.25
        _Exposure ("Exposure", Range(0.5,3)) = 1.35
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        Pass
        {
            Name "ForwardUnlitWrap"
            Tags { "LightMode"="UniversalForward" }
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float  heightOS    : TEXCOORD2;
                float  fogFactor   : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _AmbientColor;
                float _Wrap;
                float _KeyStrength;
                float _BaseShade;
                float _Exposure;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs pos = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = pos.positionCS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                // Object space height, 0 at the pivot. The assets all sit with their base on 0, so
                // this is a usable "how far up the plant am I" without needing a bounds uniform.
                OUT.heightOS = IN.positionOS.y;
                OUT.fogFactor = ComputeFogFactor(pos.positionCS.z);
                return OUT;
            }

            half4 frag (Varyings IN, FRONT_FACE_TYPE facing : FRONT_FACE_SEMANTIC) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv) * _BaseColor;

                // A leaf card seen from behind should light like one seen from the front.
                float flip = IS_FRONT_VFACE(facing, 1.0, -1.0);
                float3 N = normalize(IN.normalWS) * flip;

                Light key = GetMainLight();
                float ndl = dot(N, key.direction);
                // Wrapped diffuse: remap [-1,1] instead of clamping at 0, so backs stay readable.
                float wrapped = saturate((ndl + _Wrap) / (1.0 + _Wrap));

                float3 lighting = key.color * (wrapped * _KeyStrength) + _AmbientColor.rgb;

                // Gentle vertical falloff so the inside of a canopy is not as bright as its crown.
                float shade = lerp(1.0 - _BaseShade, 1.0, saturate(IN.heightOS * 0.55));
                lighting *= shade;

                half3 col = albedo.rgb * lighting * _Exposure;
                col = MixFog(col, IN.fogFactor);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
