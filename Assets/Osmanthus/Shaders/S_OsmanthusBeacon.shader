Shader "Osmanthus/OsmanthusBeacon"
{
    // Additive halo / ring used to mark the one osmanthus the player can actually touch.
    // _Mode 0 = soft radial glow, _Mode 1 = ring. Driven per-instance from OsmanthusBeacon.
    Properties
    {
        _BaseColor ("Color", Color) = (1.0, 0.72, 0.30, 1)
        _Alpha ("Alpha", Range(0,4)) = 1
        _Mode ("Mode (0 glow, 1 ring)", Float) = 0
        _Falloff ("Glow Falloff", Range(0.5,6)) = 2.2
        _RingRadius ("Ring Radius", Range(0,0.5)) = 0.38
        _RingWidth ("Ring Width", Range(0.005,0.3)) = 0.05
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" }

        Pass
        {
            Blend SrcAlpha One      // additive: reads as light, not as a decal
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionHCS : SV_POSITION; float2 uv : TEXCOORD0; };

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _Alpha;
                float _Mode;
                float _Falloff;
                float _RingRadius;
                float _RingWidth;
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
                float r = distance(IN.uv, float2(0.5, 0.5));
                float a;
                if (_Mode < 0.5)
                {
                    // Soft core that falls off to nothing well inside the quad edge.
                    a = pow(saturate(1.0 - r * 2.0), _Falloff);
                }
                else
                {
                    // Gaussian band at _RingRadius.
                    float d = (r - _RingRadius) / max(_RingWidth, 1e-4);
                    a = exp(-d * d);
                }
                return half4(_BaseColor.rgb * a * _Alpha, a * _Alpha);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
