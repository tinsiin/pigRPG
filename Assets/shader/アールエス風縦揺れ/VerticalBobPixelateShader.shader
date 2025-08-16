Shader "Custom/VerticalBobPixelateShader"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _Speed ("Speed", Float) = 1.0
        _Freq ("Frequency", Float) = 2.0
        _Phase ("Phase", Range(0, 6.28318)) = 0.0
        _Amplitude ("Vertical Amplitude (UV)", Range(0, 0.25)) = 0.02
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

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _MainTex_TexelSize;

            float _Speed;
            float _Freq;
            float _Phase;
            float _Amplitude;
            float _PixelationAmount;
            float _Param;

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
                // 外形は固定（頂点は動かさない）
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 縦方向にUVをオフセット（拡縮なし）
                float t = _Time.y * _Speed;
                float yOff = _Amplitude * sin(t * _Freq + _Phase);
                float2 uv = float2(i.uv.x, i.uv.y + yOff);

                // はみ出しをクランプ（縁のにじみを抑制）
                uv = clamp(uv, 0.0, 1.0);

                // ピクセル化（元シェーダの雰囲気を踏襲）
                float2 pixelSize = _MainTex_TexelSize.xy * _PixelationAmount;
                float2 puv = round(uv / pixelSize) * pixelSize;

                fixed4 col = tex2D(_MainTex, puv);
                col += tex2D(_MainTex, puv + _MainTex_TexelSize.xy * fixed2(_Param, 0));
                col += tex2D(_MainTex, puv + _MainTex_TexelSize.xy * fixed2(-_Param, 0));
                col += tex2D(_MainTex, puv + _MainTex_TexelSize.xy * fixed2(0, _Param));
                col += tex2D(_MainTex, puv + _MainTex_TexelSize.xy * fixed2(0, -_Param));

                return col / 5;
            }
            ENDCG
        }
    }
    FallBack "Transparent"
}
