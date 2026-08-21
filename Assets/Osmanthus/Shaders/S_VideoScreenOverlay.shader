Shader "Osmanthus/VideoScreenOverlay"
{
    // Surface for the head-locked video takeover. Draws over the world regardless of depth, so the
    // corridor pillars, lattice and roof cannot slice into the screen. The clear opening between the
    // corridor pillars is only ~2 m wide and the lattice tops sit at y 2.28, so a cinema-sized panel
    // can never fit inside the geometry -- it has to ignore it.
    Properties
    {
        _BaseMap ("Texture", 2D) = "white" {}
        _BaseColor ("Color", Color) = (1,1,1,1)
        _UseTexture ("Use Texture", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Overlay" }

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _UseTexture;
            CBUFFER_END

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = GetVertexPositionInputs(IN.positionOS.xyz).positionCS;
                OUT.uv = IN.uv;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half4 col = _BaseColor;
                if (_UseTexture > 0.5)
                {
                    half3 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv).rgb;
                    col.rgb *= tex;
                }
                return col;
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
