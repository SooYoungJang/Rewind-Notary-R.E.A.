Shader "RewindNotary/AtmospherePost"
{
    Properties
    {
        _MainTex ("Screen", 2D) = "white" {}
        _BloomThreshold ("Bloom Threshold", Range(0.2, 1.5)) = 0.72
        _BloomIntensity ("Bloom Intensity", Range(0.0, 1.5)) = 0.38
        _BloomSpread ("Bloom Spread", Range(0.5, 4.0)) = 1.85
        _VignetteIntensity ("Vignette Intensity", Range(0.0, 1.0)) = 0.28
        _VignetteSmoothness ("Vignette Smoothness", Range(0.1, 1.0)) = 0.55
        _GradeTint ("Grade Tint", Color) = (1, 1, 1, 1)
        _Saturation ("Saturation", Range(0.5, 1.5)) = 1.06
        _Contrast ("Contrast", Range(0.7, 1.4)) = 1.04
        _FadeAmount ("Fade Amount", Range(0.0, 1.0)) = 0.0
        _FadeColor ("Fade Color", Color) = (0.035, 0.047, 0.078, 1)
        _ImpactPulse ("Impact Pulse", Range(0.0, 1.0)) = 0.0
    }
    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #pragma target 3.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            float _BloomThreshold;
            float _BloomIntensity;
            float _BloomSpread;
            float _VignetteIntensity;
            float _VignetteSmoothness;
            fixed4 _GradeTint;
            float _Saturation;
            float _Contrast;
            float _FadeAmount;
            fixed4 _FadeColor;
            float _ImpactPulse;

            half3 SampleBright(float2 uv)
            {
                half3 c = tex2D(_MainTex, uv).rgb;
                half lum = dot(c, half3(0.2126, 0.7152, 0.0722));
                half soft = saturate((lum - _BloomThreshold) / max(0.001, 1.0 - _BloomThreshold));
                return c * (soft * soft);
            }

            fixed4 frag(v2f_img i) : SV_Target
            {
                float2 uv = i.uv;
                float2 centered = uv - 0.5;
                float dist = length(centered);

                half3 baseCol;
                if (_ImpactPulse > 0.001)
                {
                    float2 ca = centered * (_ImpactPulse * 0.009);
                    baseCol.r = tex2D(_MainTex, uv + ca).r;
                    baseCol.g = tex2D(_MainTex, uv).g;
                    baseCol.b = tex2D(_MainTex, uv - ca).b;
                }
                else
                {
                    baseCol = tex2D(_MainTex, uv).rgb;
                }

                float2 step1 = _MainTex_TexelSize.xy * _BloomSpread * 1.4;
                float2 step2 = _MainTex_TexelSize.xy * _BloomSpread * 3.1;
                half3 bloom = SampleBright(uv) * 0.20;
                bloom += (SampleBright(uv + float2(step1.x, step1.y))
                        + SampleBright(uv + float2(-step1.x, step1.y))
                        + SampleBright(uv + float2(step1.x, -step1.y))
                        + SampleBright(uv + float2(-step1.x, -step1.y))) * 0.11;
                bloom += (SampleBright(uv + float2(step2.x, 0))
                        + SampleBright(uv + float2(-step2.x, 0))
                        + SampleBright(uv + float2(0, step2.y))
                        + SampleBright(uv + float2(0, -step2.y))) * 0.09;

                half3 col = baseCol + bloom * _BloomIntensity;

                col *= _GradeTint.rgb;
                half gray = dot(col, half3(0.299, 0.587, 0.114));
                col = lerp(half3(gray, gray, gray), col, _Saturation);
                col = (col - 0.5) * _Contrast + 0.5;
                col = saturate(col);

                float vig = smoothstep(0.42, 0.42 + _VignetteSmoothness, dist * 1.32);
                col *= 1.0 - vig * _VignetteIntensity;

                col = lerp(col, col + half3(0.18, 0.15, 0.10), _ImpactPulse * 0.35);
                col = lerp(col, _FadeColor.rgb, saturate(_FadeAmount));
                return fixed4(col, 1.0);
            }
            ENDCG
        }
    }
}
