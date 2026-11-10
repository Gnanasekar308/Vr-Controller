// Selection outline for the phone's Live Scene view (Built-in pipeline). Used as a SECOND material on the
// selected object's renderer: the mesh is pushed outwards from the object's centre (constant width on screen)
// and only its back faces are drawn, so a coloured rim shows around the object.
Shader "MediSimVR/Outline"
{
    Properties
    {
        _Color ("Color", Color) = (1, 0.62, 0.10, 1)
        _Width ("Width (fraction of distance)", Float) = 0.012
    }
    SubShader
    {
        Tags { "Queue" = "Geometry+10" "RenderType" = "Opaque" }
        Pass
        {
            Cull Front
            ZWrite On
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Width;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; };

            v2f vert (appdata v)
            {
                v2f o;
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float3 c = float3(unity_ObjectToWorld._m03, unity_ObjectToWorld._m13, unity_ObjectToWorld._m23);
                float3 d = wp - c;
                float len = length(d);
                float3 dir = len > 1e-6 ? d / len : float3(0, 1, 0);
                float dist = distance(_WorldSpaceCameraPos.xyz, wp);
                wp += dir * (_Width * dist);
                o.pos = mul(UNITY_MATRIX_VP, float4(wp, 1.0));
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return _Color; }
            ENDCG
        }
    }
    Fallback Off
}
