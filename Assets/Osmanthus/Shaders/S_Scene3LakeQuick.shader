Shader "Osmanthus/Scene3LakeQuick"
{
    Properties
    {
        _NearColor ("Near Color", Color) = (0.10,0.17,0.20,1)
        _FarColor ("Far Color", Color) = (0.28,0.38,0.40,1)
        _HorizonColor ("Horizon Glow", Color) = (0.82,0.70,0.54,1)
        _ReflectColor ("Fake Reflection", Color) = (0.85,0.60,0.38,1)
        _ReflectStrength ("Reflection Strength", Range(0,1)) = 0.35
        _RippleColor ("Ripple Tint", Color) = (0.90,0.95,1.0,1)
        _RippleStrength ("Ripple Strength", Range(0,1)) = 0.10
        _RippleScale ("Ripple Scale", Float) = 0.35
        _RippleSpeed ("Ripple Speed", Float) = 0.35
        _GradFront ("Grad Front X", Float) = -6
        _GradBack ("Grad Back X", Float) = -52
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
            struct Varyings { float4 positionHCS : SV_POSITION; float3 positionWS : TEXCOORD0; float2 uv : TEXCOORD1; };

            CBUFFER_START(UnityPerMaterial)
                float4 _NearColor;
                float4 _FarColor;
                float4 _HorizonColor;
                float4 _ReflectColor;
                float _ReflectStrength;
                float4 _RippleColor;
                float _RippleStrength;
                float _RippleScale;
                float _RippleSpeed;
                float _GradFront;
                float _GradBack;
            CBUFFER_END

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
                float t = saturate((wp.x - _GradFront) / (_GradBack - _GradFront)); // 0 near cam -> 1 far horizon
                half3 col = lerp(_NearColor.rgb, _FarColor.rgb, t);

                // horizon warm glow toward the far edge
                float horizon = smoothstep(0.72, 1.0, t);
                col = lerp(col, _HorizonColor.rgb, horizon * 0.6);

                // scrolling ripple (two crossed sine waves, world-space, animated)
                float tm = _Time.y * _RippleSpeed;
                float r1 = sin((wp.x + wp.z) * _RippleScale + tm);
                float r2 = sin((wp.x * 0.7 - wp.z * 1.3) * _RippleScale * 1.7 - tm * 1.3);
                float ripple = (r1 + r2) * 0.5; // -1..1
                col += _RippleColor.rgb * ripple * _RippleStrength * (1.0 - t * 0.5);

                // fake reflection band just in front of the islands/horizon
                float refl = smoothstep(0.45, 0.62, t) * (1.0 - smoothstep(0.62, 0.82, t));
                col = lerp(col, _ReflectColor.rgb, refl * _ReflectStrength * (0.6 + 0.4 * ripple));

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
