// 塔外框的 Stencil 遮罩：不輸出顏色、不寫深度，只把被選取塔「看得到的像素」在 Stencil 標記為 1。
// 佇列 Geometry+5，排在塔本身（Geometry）之後、外殼（TowerOutline，Geometry+10）之前，
// 讓外殼只畫在 Stencil 不是 1 的地方，去掉部位交界處的內部線條，只留整座塔的外輪廓。
Shader "CyberWizard/TowerOutlineMask"
{
    Properties
    {
        _StencilRef("Stencil Reference", Range(1, 255)) = 1
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry+5"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            Name "OutlineMask"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull Back
            ZWrite Off
            ZTest LEqual
            ColorMask 0
            // 與塔本身是同一個網格、同一個位置，往鏡頭偏一點避免深度相等時判定失敗。
            Offset -1, -1

            Stencil
            {
                Ref [_StencilRef]
                Comp Always
                Pass Replace
            }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _StencilRef;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                return 0;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
