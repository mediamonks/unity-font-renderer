Shader "MediaMonks/StencilWrite/QuadraticBezier"
{
	Properties
	{
	}
		SubShader
	{
		Tags { "RenderType" = "Opaque" "Queue" = "Geometry-1"}

		Pass
		{
			Stencil
			{
				WriteMask 1
				Comp Always
				Pass Invert
			}

			Blend Zero One
			ZTest Always Cull Off ZWrite Off

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag

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
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
				clip(-(i.uv.x * i.uv.x - i.uv.y));
				return 0;
            }
            ENDCG
        }
    }
}
