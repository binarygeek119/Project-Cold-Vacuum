Shader "Space/RingBillboard"
{
    Properties
    {
        _MainTex ("Ring Texture", 2D) = "white" {}
        _Color ("Ring Color", Color) = (1, 1, 1, 1)
        _InnerRadius ("Inner Radius", Float) = 0.5
        _OuterRadius ("Outer Radius", Float) = 1.0
        _Tilt ("Ring Tilt", Range(0, 90)) = 0
        _AlphaCutoff ("Alpha Cutoff", Range(0, 1)) = 0.1
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 200
        
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldPos : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float _InnerRadius;
            float _OuterRadius;
            float _Tilt;
            float _AlphaCutoff;
            
            v2f vert (appdata v)
            {
                v2f o;
                
                // Apply tilt rotation
                float tiltRad = radians(_Tilt);
                float3 tiltedPos = v.vertex.xyz;
                tiltedPos.y = tiltedPos.y * cos(tiltRad) - tiltedPos.z * sin(tiltRad);
                tiltedPos.z = v.vertex.y * sin(tiltRad) + v.vertex.z * cos(tiltRad);
                
                o.vertex = UnityObjectToClipPos(float4(tiltedPos, 1));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, float4(tiltedPos, 1)).xyz;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate ring shape based on UV coordinates
                float2 uv = i.uv * 2.0 - 1.0;
                float dist = length(uv);
                
                // Ring mask: between inner and outer radius
                float ringMask = smoothstep(_InnerRadius - 0.05, _InnerRadius, dist) * 
                                (1.0 - smoothstep(_OuterRadius, _OuterRadius + 0.05, dist));
                
                // Sample texture
                fixed4 tex = tex2D(_MainTex, i.uv);
                
                // Apply ring shape and color
                fixed4 col = tex * _Color;
                col.a *= ringMask;
                
                // Alpha cutoff
                clip(col.a - _AlphaCutoff);
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
