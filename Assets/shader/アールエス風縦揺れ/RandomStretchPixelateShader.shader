Shader "Custom/RandomStretchPixelateShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _ScaleRangeX ("Scale Range X (min,max)", Vector) = (0.8, 1.2, 0, 0)
        _ScaleRangeY ("Scale Range Y (min,max)", Vector) = (0.8, 1.2, 0, 0)
        _Speed ("Speed", Float) = 1.0
        _FreqX ("Frequency X", Float) = 2.0
        _FreqY ("Frequency Y", Float) = 3.0
        _PhaseX ("Phase X", Range(0, 6.28)) = 0.0
        _PhaseY ("Phase Y", Range(0, 6.28)) = 0.0
        _PixelationAmount ("Pixelation Amount", Range(1, 100)) = 10
        _Param ("Param", Range(0, 1)) = 0.1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            // プロパティ宣言
            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _ScaleRangeX;
            float4 _ScaleRangeY;
            float _Speed;
            float _FreqX;
            float _FreqY;
            float _PhaseX;
            float _PhaseY;
            float _PixelationAmount;
            float _Param;
            float4 _MainTex_TexelSize;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;

                // 時間の取得と速度の適用
                float t = _Time.y * _Speed;
                
                // サイン波を用いたスケール計算
                float scaleX = lerp(_ScaleRangeX.x, _ScaleRangeX.y, 0.5 + 0.5 * sin(t * _FreqX + _PhaseX));
                float scaleY = lerp(_ScaleRangeY.x, _ScaleRangeY.y, 0.5 + 0.5 * sin(t * _FreqY + _PhaseY));//同じ計算してるからそのまま使用

                // 頂点の位置をスケーリング
                float3 pos = v.vertex.xyz;
                pos.x *= scaleX;
                pos.y *= scaleY;

                // 頂点の位置とUV座標を設定
                o.vertex = UnityObjectToClipPos(float4(pos, 1.0));
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // ピクセルサイズに基づいたピクセル化効果
                float2 pixelSize = _MainTex_TexelSize.xy * _PixelationAmount;
                float2 uv = round(i.uv / pixelSize) * pixelSize;

                // ピクセル化されたテクスチャのサンプリング
                fixed4 col = tex2D(_MainTex, uv);
                col += tex2D(_MainTex, uv + _MainTex_TexelSize.xy * fixed2(_Param, 0));
                col += tex2D(_MainTex, uv + _MainTex_TexelSize.xy * fixed2(-_Param, 0));
                col += tex2D(_MainTex, uv + _MainTex_TexelSize.xy * fixed2(0, _Param));
                col += tex2D(_MainTex, uv + _MainTex_TexelSize.xy * fixed2(0, -_Param));

                // 色の平均を返す
                return col / 5;
            }
            ENDCG
        }
    }
    FallBack "Transparent"
}
