Shader "Custom/PSX_CRT_Degradation"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _BlitTexture ("Blit Texture", 2D) = "white" {}
        _PixelSize ("Pixel Size", Range(1, 16)) = 1
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0
        _ChromaticAberration ("Chromatic Aberration", Range(0, 0.05)) = 0
        _CRTCurvature ("CRT Curvature", Range(0, 0.2)) = 0
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0
        _ColorDepth ("Color Depth", Range(2, 32)) = 32
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        LOD 100
        ZTest Always ZWrite Off Cull Off

        Pass
        {
            Name "PSX_CRT_Pass"

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            TEXTURE2D(_BlitTexture);
            SAMPLER(sampler_BlitTexture);

            float _PixelSize;
            float _ScanlineIntensity;
            float _ChromaticAberration;
            float _CRTCurvature;
            float _VignetteIntensity;
            float _ColorDepth;

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = GetFullScreenTriangleVertexPosition(input.vertexID);
                output.uv = GetFullScreenTriangleTexCoord(input.vertexID);
                return output;
            }

            float2 CurveUV(float2 uv)
            {
                if (_CRTCurvature <= 0.001) return uv;
                float2 centeredUV = uv - 0.5;
                float dist = dot(centeredUV, centeredUV);
                centeredUV *= 1.0 + dist * _CRTCurvature;
                return centeredUV + 0.5;
            }

            float4 SampleColor(float2 uv)
            {
                float4 c1 = SAMPLE_TEXTURE2D(_BlitTexture, sampler_BlitTexture, uv);
                float4 c2 = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
                return max(c1, c2);
            }

            float4 frag(Varyings input) : SV_Target
            {
                float2 uv = CurveUV(input.uv);

                // Darken area outside CRT curvature bounds
                if (uv.x < 0.0 || uv.x > 1.0 || uv.y < 0.0 || uv.y > 1.0)
                {
                    return float4(0, 0, 0, 1);
                }

                // PSX Pixelation
                if (_PixelSize > 1.001)
                {
                    float2 res = _ScreenParams.xy / _PixelSize;
                    uv = floor(uv * res) / res;
                }

                // Chromatic Aberration
                float3 col;
                if (_ChromaticAberration > 0.0001)
                {
                    float2 distFromCenter = uv - 0.5;
                    float2 offset = distFromCenter * _ChromaticAberration;
                    col.r = SampleColor(uv + offset).r;
                    col.g = SampleColor(uv).g;
                    col.b = SampleColor(uv - offset).b;
                }
                else
                {
                    col = SampleColor(uv).rgb;
                }

                // Color Depth Quantization
                if (_ColorDepth < 31.0)
                {
                    float steps = pow(2.0, _ColorDepth);
                    col = floor(col * steps) / steps;
                }

                // CRT Scanlines
                if (_ScanlineIntensity > 0.001)
                {
                    float scanline = sin(uv.y * _ScreenParams.y * 1.5) * 0.5 + 0.5;
                    scanline = lerp(1.0, scanline, _ScanlineIntensity);
                    col *= scanline;
                }

                // CRT Vignette
                if (_VignetteIntensity > 0.001)
                {
                    float2 distFromCenter = (uv - 0.5) * 2.0;
                    float vignette = 1.0 - dot(distFromCenter, distFromCenter) * (_VignetteIntensity * 0.5);
                    col *= saturate(vignette);
                }

                return float4(col, 1.0);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
