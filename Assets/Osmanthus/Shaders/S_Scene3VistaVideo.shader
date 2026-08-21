Shader "Osmanthus/Scene3VistaVideo"
{
    // Unlit backdrop card for the lake vista video. Crops the source (the generated clip carries a
    // watermark in the top-right), feathers its borders so the card dissolves into the painted far
    // backdrop instead of reading as a rectangle, and exposes _Alpha for the loop crossfade.
    Properties
    {
        _BaseMap ("Video / Poster", 2D) = "black" {}
        _BaseColor ("Tint", Color) = (1,1,1,1)
        _Alpha ("Alpha", Range(0,1)) = 1
        _Exposure ("Exposure", Range(0,3)) = 1
        _Desaturate ("Desaturate", Range(0,1)) = 0
        _HazeColor ("Distance Haze", Color) = (0.78,0.74,0.66,1)
        _HazeAmount ("Haze Amount", Range(0,1)) = 0.12
        _CropLeft ("Crop Left", Range(0,0.4)) = 0
        _CropRight ("Crop Right", Range(0,0.4)) = 0.055
        _CropBottom ("Crop Bottom", Range(0,0.4)) = 0
        _CropTop ("Crop Top", Range(0,0.4)) = 0.045
        _FeatherX ("Feather Sides", Range(0,0.5)) = 0.16
        _FeatherTop ("Feather Top", Range(0,0.5)) = 0.14
        _FeatherBottom ("Feather Bottom", Range(0,0.5)) = 0.06

        // Approach fade. The card is composed for the lake terrace; now that the courtyard is open
        // it is also visible side-on from the corridor, where it reads as a lit billboard hanging
        // over the garden. Fading it by the viewer's distance from the terrace keeps it a faint
        // suggestion until you actually walk out to the water. Set _MinAlpha to 1 to disable.
        _FocusPoint ("Focus Point", Vector) = (-9.2, 1.8, 20, 0)
        _FocusNear ("Full Strength Within", Float) = 10
        _FocusFar ("Faded Beyond", Float) = 32
        _MinAlpha ("Distant Alpha", Range(0,1)) = 0.15
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

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float4 _HazeColor;
                float _Alpha;
                float _Exposure;
                float _Desaturate;
                float _HazeAmount;
                float _CropLeft;
                float _CropRight;
                float _CropBottom;
                float _CropTop;
                float _FeatherX;
                float _FeatherTop;
                float _FeatherBottom;
                float4 _FocusPoint;
                float _FocusNear;
                float _FocusFar;
                float _MinAlpha;
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
                // Card UV (0..1 across the arc) -> cropped source UV.
                float2 src;
                src.x = lerp(_CropLeft, 1.0 - _CropRight, IN.uv.x);
                src.y = lerp(_CropBottom, 1.0 - _CropTop, IN.uv.y);
                half3 col = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, src).rgb;

                col *= _Exposure;
                col = lerp(col, dot(col, half3(0.299, 0.587, 0.114)).xxx, _Desaturate);
                col = lerp(col, _HazeColor.rgb, _HazeAmount);
                col *= _BaseColor.rgb;

                // Feathered borders: soft on the sides and top, tighter at the bottom where the
                // waterline haze already takes over.
                float fx = smoothstep(0.0, max(_FeatherX, 1e-4), IN.uv.x)
                         * smoothstep(0.0, max(_FeatherX, 1e-4), 1.0 - IN.uv.x);
                float ft = smoothstep(0.0, max(_FeatherTop, 1e-4), 1.0 - IN.uv.y);
                float fb = smoothstep(0.0, max(_FeatherBottom, 1e-4), IN.uv.y);

                // Resolve as the viewer approaches the terrace.
                float viewerDist = distance(_WorldSpaceCameraPos, _FocusPoint.xyz);
                float near = 1.0 - smoothstep(_FocusNear, max(_FocusFar, _FocusNear + 1e-3), viewerDist);
                float approach = lerp(_MinAlpha, 1.0, near);

                return half4(col, saturate(_Alpha * _BaseColor.a * fx * ft * fb * approach));
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
