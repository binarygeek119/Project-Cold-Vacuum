Shader "Space/StarMesh"
{
    Properties
    {
        _MainTex ("Surface Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1, 0.9, 0.7, 1)
        _EmissionColor ("Emission Color", Color) = (1, 0.8, 0.5, 1)
        _EmissionIntensity ("Emission Intensity", Float) = 3.0
        _GlowColor ("Glow Color", Color) = (1, 0.9, 0.7, 1)
        _GlowIntensity ("Glow Intensity", Float) = 1.5
        _SurfaceDetail ("Surface Detail", Float) = 0.5
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 300
        
        Pass
        {
            Name "FORWARD"
            Tags { "LightMode" = "ForwardBase" }
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };
            
            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD1;
                float3 worldPos : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _EmissionColor;
            float _EmissionIntensity;
            float4 _GlowColor;
            float _GlowIntensity;
            float _SurfaceDetail;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Sample surface texture
                fixed4 tex = tex2D(_MainTex, i.uv);
                
                // Base color
                fixed4 col = tex * _Color;
                
                // Surface detail variation
                float detail = tex2D(_MainTex, i.uv * 10.0).r * _SurfaceDetail;
                col.rgb += detail * 0.1;
                
                // Emission (star surface glow)
                col.rgb += _EmissionColor.rgb * _EmissionIntensity * tex.a;
                
                // Fresnel-like rim glow
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fresnel = 1.0 - saturate(dot(viewDir, i.worldNormal));
                fresnel = pow(fresnel, 2.0);
                col.rgb += _GlowColor.rgb * _GlowIntensity * fresnel;
                
                // Apply fog
                UNITY_APPLY_FOG(i.fogCoord, col);
                
                return col;
            }
            ENDCG
        }
        
        // Additional pass for additive glow
        Pass
        {
            Name "GLOW"
            Tags { "LightMode" = "ForwardAdd" }
            Blend One One
            ZWrite Off
            
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            
            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };
            
            float4 _GlowColor;
            float _GlowIntensity;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float fresnel = 1.0 - saturate(dot(viewDir, i.worldNormal));
                fresnel = pow(fresnel, 3.0);
                
                fixed4 col = _GlowColor * _GlowIntensity * fresnel;
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
