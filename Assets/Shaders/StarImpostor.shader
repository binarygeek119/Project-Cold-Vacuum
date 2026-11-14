Shader "Space/StarImpostor"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Color ("Color", Color) = (1, 1, 1, 1)
        _EmissionColor ("Emission Color", Color) = (1, 0.9, 0.7, 1)
        _EmissionIntensity ("Emission Intensity", Float) = 2.0
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
                UNITY_FOG_COORDS(2)
            };
            
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _Color;
            float4 _EmissionColor;
            float _EmissionIntensity;
            float _BillboardSize;
            
            v2f vert (appdata v)
            {
                v2f o;
                
                // Billboard transformation - always face camera
                float3 cameraPos = _WorldSpaceCameraPos;
                float3 objectPos = mul(unity_ObjectToWorld, float4(0, 0, 0, 1)).xyz;
                float3 toCamera = normalize(cameraPos - objectPos);
                float3 up = float3(0, 1, 0);
                float3 right = normalize(cross(up, toCamera));
                up = cross(toCamera, right);
                
                // Scale vertex position for billboard
                float3 billboardPos = objectPos + (right * v.vertex.x + up * v.vertex.y) * _BillboardSize;
                
                o.vertex = UnityWorldToClipPos(float4(billboardPos, 1));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldNormal = toCamera;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 tex = tex2D(_MainTex, i.uv);
                fixed4 col = tex * _Color;
                
                // Add emission
                col.rgb += _EmissionColor.rgb * _EmissionIntensity * tex.a;
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
