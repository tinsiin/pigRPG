Shader "Custom/CentralFadeReverseShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _OverallTransparency ("Overall Transparency", Range(0,1)) = 1.0
        _FadeRadius ("Fade Radius", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float _OverallTransparency;
            float _FadeRadius;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float2 centerDist : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                // UVの中心からの距離を計算（0:中心, ~0.707:隅）
                float2 center = float2(0.5, 0.5);
                o.centerDist = abs(v.uv - center) * 1.414; // 正規化
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 texColor = tex2D(_MainTex, i.uv);
                
                // 中心からの距離に基づいて透明度を調整
                float distance = length(i.uv - float2(0.5, 0.5)) / _FadeRadius;
                distance = saturate(distance); // 0から1にクランプ

                // 透明度を逆転させる
                // distanceが0のときalphaは0（完全透明）
                // distanceが1のときalphaは_OverallTransparency（不透明度）
                float alpha = lerp(0.0, _OverallTransparency, distance);

                texColor.a *= alpha;
                return texColor;
            }
            ENDCG
        }
    }
    FallBack "Diffuse"
}
