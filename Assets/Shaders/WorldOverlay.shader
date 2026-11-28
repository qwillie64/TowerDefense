// 永遠畫在最上層的半透明世界空間面板（例如塔的 3D 升級提示），不會被建築或塔擋住。
// ZTest Always、ZWrite Off、Overlay 佇列。
// 顏色 = _BaseColor × 貼圖 Alpha（_MainTex；預設白色）× 頂點色（_UseVertexColor 為 1 時）。
// TextMesh 的字型貼圖只有 Alpha 通道，文字顏色由頂點色提供，所以文字材質需開啟 _UseVertexColor。
Shader "CyberWizard/WorldOverlay"
{
    Properties
    {
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _MainTex("Alpha Texture", 2D) = "white" {}
        [Toggle] _UseVertexColor("Use Vertex Color", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Overlay"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "Overlay"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float4 _MainTex_ST;
                float _UseVertexColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = lerp(half4(1, 1, 1, 1), input.color, (half)_UseVertexColor);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                half4 color = _BaseColor * input.color;
                color.a *= SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv).a;
                return color;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
