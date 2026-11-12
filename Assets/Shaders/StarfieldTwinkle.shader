Shader "Custom/StarfieldTwinkle"
{
    Properties
    {
        _BaseMap ("Star Texture", 2D) = "black" {}
        _BaseColor ("Base Color", Color) = (1,1,1,1)
        _TwinkleSpeed ("Twinkle Speed", Float) = 1.5
        _TwinkleAmount ("Twinkle Amount", Range(0,1)) = 0.6
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Background" }
        Pass
        {
            Name "ForwardUnlit"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                float4 _BaseColor;
                float _TwinkleSpeed;
                float _TwinkleAmount;
            CBUFFER_END

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _BaseMap);
                return OUT;
            }

            float hash(float2 p) { return frac(sin(dot(p, float2(12.9898, 78.233))) * 43758.5453); }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 tex = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, IN.uv);
                float n = hash(IN.uv * 200.0);
                // per-star phase from a second hash so stars twinkle out of sync
                float phase = hash(IN.uv * 37.0) * 6.28318;
                float twinkle = 1.0 + _TwinkleAmount * sin(_Time.y * _TwinkleSpeed + phase + n * 3.0);
                // occasional brighter flash
                float flash = step(0.97, hash(IN.uv * 13.0 + floor(_Time.y * 0.5)));
                twinkle += flash * 1.5;
                half3 color = tex.rgb * _BaseColor.rgb * twinkle;
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
    FallBack "Universal Render Pipeline/Unlit"
}
