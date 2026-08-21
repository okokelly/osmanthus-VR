Shader "Osmanthus/Scene3LakeQuick"
{
    Properties
    {
        _NearColor ("Near Color", Color) = (0.10,0.17,0.20,1)
        _FarColor ("Far Color", Color) = (0.28,0.38,0.40,1)
        _HorizonColor ("Horizon Glow", Color) = (0.82,0.70,0.54,1)
        _ReflectColor ("Fake Reflection", Color) = (0.85,0.60,0.38,1)
        _ReflectStrength ("Reflection Strength", Range(0,1)) = 0.35
        _ReflectWidth ("Reflection Column Width", Range(1,120)) = 26
        _RippleColor ("Ripple Tint", Color) = (0.90,0.95,1.0,1)
        _RippleStrength ("Ripple Strength", Range(0,1)) = 0.10
        _RippleScale ("Ripple Scale", Float) = 0.35
        _RippleSpeed ("Ripple Speed", Float) = 0.35
        _RippleFalloff ("Ripple Distance Falloff", Range(0.5,8)) = 2.6
        _DetailStrength ("Detail Ripple Strength", Range(0,1)) = 0.05
        _DetailScale ("Detail Ripple Scale", Float) = 4.0
        _GlitterStrength ("Glitter Strength", Range(0,1)) = 0.12
        _GlitterScale ("Glitter Scale", Float) = 1.6
        _CamFadeStart ("Surface Detail Fade Start", Float) = 16
        _CamFadeEnd ("Surface Detail Fade End", Float) = 52
        _HazeColor ("Horizon Haze", Color) = (0.78,0.76,0.70,1)
        _HazeStart ("Haze Start", Range(0,1)) = 0.55
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
                float _ReflectWidth;
                float4 _RippleColor;
                float _RippleStrength;
                float _RippleScale;
                float _RippleSpeed;
                float _RippleFalloff;
                float _DetailStrength;
                float _DetailScale;
                float _GlitterStrength;
                float _GlitterScale;
                float _CamFadeStart;
                float _CamFadeEnd;
                float4 _HazeColor;
                float _HazeStart;
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

            // Cheap value noise, used to break the regularity of the crossed waves so they read as
            // water rather than as corduroy.
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

            half4 frag (Varyings IN) : SV_Target
            {
                float3 wp = IN.positionWS;
                float t = saturate((wp.x - _GradFront) / (_GradBack - _GradFront)); // 0 near cam -> 1 far horizon

                half3 col = lerp(_NearColor.rgb, _FarColor.rgb, t);

                // Warm horizon glow just short of the far edge.
                float horizon = smoothstep(0.72, 1.0, t);
                col = lerp(col, _HorizonColor.rgb, horizon * 0.6);

                // --- Surface motion -------------------------------------------------------------
                // Three waves at unrelated angles and wavelengths, warped by low-frequency noise, so
                // no single direction dominates. Amplitude dies off with distance: far water in a
                // perspective view is a smooth sheet, and holding ripple size constant out to the
                // horizon is what reads as stripes.
                float tm = _Time.y * _RippleSpeed;
                float2 p = wp.xz * _RippleScale;
                float warp = ValueNoise(p * 0.35 + tm * 0.12) - 0.5;
                p += warp * 1.6;

                float w1 = sin(dot(p, float2(0.94, 0.34)) + tm);
                float w2 = sin(dot(p, float2(-0.42, 0.91)) * 1.63 - tm * 1.27);
                float w3 = sin(dot(p, float2(0.61, -0.79)) * 2.37 + tm * 0.71);
                float ripple = (w1 * 0.5 + w2 * 0.33 + w3 * 0.17); // -1..1, no repeating beat

                // The x-gradient is not the same thing as screen distance: from the terrace almost
                // the whole visible sheet is far water compressed into a few degrees, and per-pixel
                // value noise at that grazing angle aliases into a moire checkerboard. Everything
                // high-frequency is additionally damped by true camera distance.
                float dcam = distance(wp, _WorldSpaceCameraPos);
                float crisp = 1.0 - smoothstep(_CamFadeStart, max(_CamFadeEnd, _CamFadeStart + 1e-3), dcam);

                float nearness = pow(saturate(1.0 - t), _RippleFalloff) * crisp;
                col += _RippleColor.rgb * ripple * _RippleStrength * nearness;

                // Second, finer octave. On a flat plane at eye height the near field takes up most
                // of the screen, so without this the water right in front of the viewer is a blank
                // slab. It dies off much faster than the swell so it never reaches the horizon.
                float2 dp = wp.xz * _DetailScale + warp;
                float d1 = sin(dot(dp, float2(0.83, 0.56)) + tm * 2.1);
                float d2 = sin(dot(dp, float2(-0.55, 0.84)) * 1.41 - tm * 1.7);
                float detail = d1 * 0.6 + d2 * 0.4;
                col += _RippleColor.rgb * detail * _DetailStrength * pow(saturate(1.0 - t), 6.0) * crisp;

                // Sparse glitter on the wave crests.
                float crest = saturate((ripple + detail * 0.5) * 0.5 + 0.5);
                float sparkle = ValueNoise(wp.xz * _GlitterScale + tm * 0.4);
                float glint = pow(saturate(crest * sparkle), 12.0);
                col += _RippleColor.rgb * glint * _GlitterStrength * nearness;

                // --- Reflected light column -----------------------------------------------------
                // A soft warm smear running from the horizon all the way to the viewer's feet, under
                // the vista, broken up by the wave field so it wobbles rather than sitting there as
                // a painted stripe. It widens as it approaches, the way a real glitter path does.
                float width = _ReflectWidth * (1.0 - 0.55 * t);
                float column = exp(-(wp.z - 20.0) * (wp.z - 20.0) / (width * width));
                // Weakest at the viewer's feet and strongest mid-lake, otherwise the column washes
                // the whole near surface warm and the water stops reading as water.
                float band = (0.35 + 0.65 * saturate(t * 2.2)) * (1.0 - smoothstep(0.86, 1.0, t));
                float refl = column * band * (0.45 + 0.55 * crest);
                col = lerp(col, _ReflectColor.rgb, saturate(refl * _ReflectStrength));

                // --- Horizon haze ----------------------------------------------------------------
                // Dissolves the far edge of the plane into the painted backdrop so the two do not
                // meet as a hard tonal line.
                float haze = smoothstep(_HazeStart, 1.0, t);
                col = lerp(col, _HazeColor.rgb, haze * _HazeColor.a);

                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback "Universal Render Pipeline/Unlit"
}
