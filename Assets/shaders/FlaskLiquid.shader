Shader "ChemLabs/FlaskLiquid"
{
    Properties
    {
        _BaseColor ("Liquid Color", Color) = (0.75, 0.90, 1.0, 0.70)
        _SurfaceColor ("Top Surface Color", Color) = (0.85, 0.95, 1.0, 0.85)
        _FillPlaneY ("Fill World Y", Float) = 0.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent-1" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        Cull Off // Menampilkan permukaan atas cairan saat bagian atas mesh terpotong

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPos : TEXCOORD0;
            };

            fixed4 _BaseColor;
            fixed4 _SurfaceColor;
            float _FillPlaneY;

            v2f vert (appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                return o;
            }

            fixed4 frag (v2f i, fixed facing : VFACE) : SV_Target
            {
                // Buang/potong seluruh bagian cairan yang posisinya di atas garis ketinggian _FillPlaneY
                clip(_FillPlaneY - i.worldPos.y);

                // Sisi luar memakai _BaseColor, sisi dalam (permukaan atas cairan) memakai _SurfaceColor
                return facing > 0 ? _BaseColor : _SurfaceColor;
            }
            ENDCG
        }
    }
}