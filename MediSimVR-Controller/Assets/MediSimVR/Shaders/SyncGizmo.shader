// Move / rotate / scale handles of the phone's Live Scene view (Built-in pipeline): flat colour (per renderer via
// MaterialPropertyBlock "_Color"), drawn after everything and on top of everything, so a handle is never hidden.
Shader "MediSimVR/Gizmo"
{
    Properties
    {
        _Color ("Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "Queue" = "Overlay+10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        Pass
        {
            Cull Back
            ZWrite Off
            ZTest Always
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _Color;

            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f { float4 pos : SV_POSITION; fixed4 col : COLOR; };

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                // a little shading so arrows / rings read as 3D
                float3 n = normalize(mul((float3x3)unity_ObjectToWorld, v.normal));
                float3 viewDir = normalize(_WorldSpaceCameraPos.xyz - mul(unity_ObjectToWorld, v.vertex).xyz);
                float k = 0.72 + 0.28 * saturate(dot(n, viewDir));
                o.col = fixed4(_Color.rgb * k, _Color.a);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target { return i.col; }
            ENDCG
        }
    }
    Fallback Off
}
