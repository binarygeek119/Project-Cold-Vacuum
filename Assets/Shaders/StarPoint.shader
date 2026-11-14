Shader "Space/StarPoint"
{
    Properties
    {
        _Color ("Star Color", Color) = (1, 1, 1, 1)
        _Size ("Point Size", Float) = 1.0
        _Brightness ("Brightness", Float) = 1.0
    }
    
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        
        Pass
        {
            Blend SrcAlpha One
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
                float size : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };
            
            float4 _Color;
            float _Size;
            float _Brightness;
            
            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.size = _Size;
                UNITY_TRANSFER_FOG(o, o.vertex);
                return o;
            }
            
            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate distance from center
                float2 uv = i.uv * 2.0 - 1.0;
                float dist = length(uv);
                
                // Star glow falloff
                float glow = exp(-dist * 3.0) * _Brightness;
                
                // Apply color and alpha
                fixed4 col = _Color * glow;
                col.a = glow;
                
                UNITY_APPLY_FOG(i.fogCoord, col);
                return col;
            }
            ENDCG
        }
    }
}
