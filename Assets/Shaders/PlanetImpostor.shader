Shader "Space/PlanetImpostor"
{
    Properties
    {
        _MainTex ("Planet Texture", 2D) = "white" {}
        _Color ("Base Color", Color) = (1, 1, 1, 1)
        _AtmosphereColor ("Atmosphere Color", Color) = (0.3, 0.6, 1.0, 1)
        _AtmosphereIntensity ("Atmosphere Intensity", Float) = 0.5
        _BillboardSize ("Billboard Size", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            
            #include "UnityCG.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL;
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
            float4 _AtmosphereColor;
            float _AtmosphereIntensity;
            float _BillboardSize;
            
            v2f vert (appdata v)
            {
                v2f o;
                
                // Billboard transformation
                float3 cameraPos = _WorldSpaceCameraPos;
                float3 objectPos = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 toCamera = normalize(cameraPos - objectPos);
                float3 up = float3(0, 1, 0);
                float3 right = normalize(cross(up, toCamera));
                up = cross(toCamera, right);
                
                float3 billboardPos = objectPos + (right * v.vertex.x + up * v.vertex.y) * _BillboardSize;
                
                o.vertex = UnityWorldToClipPos(float4(billboardPos, 1));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = toCamera;
                o.worldPos = billboardPos;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Sample planet texture
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 col = tex * _Color;
                
                // Calculate atmosphere rim effect
                float2 uv = i.uv * 2.0 - 1.0;
                float dist = length(uv);
                float rim = smoothstep(0.7, 1.0, dist);
                
                // Add atmosphere glow
                col.rgb = lerp(col.rgb, _AtmosphereColor.rgb, rim * _AtmosphereIntensity);
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
