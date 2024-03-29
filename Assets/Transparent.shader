﻿Shader "KT/Transparent/Transparent" {
Properties {
	_MainTex ("Base (RGB) Trans (A)", 2D) = "white" {}
	_Color ("Color", Color) = (1, 1, 1, 1)
}
	SubShader {
		Tags {"Queue"="Transparent-200" "IgnoreProjector"="True" "RenderType"="Transparent"}
		Lighting Off Cull Back ZTest LEqual ZWrite Off Fog { Mode Off }
		Blend SrcAlpha OneMinusSrcAlpha
		LOD 150

		CGPROGRAM
			#pragma surface surf Lambert alpha

			uniform sampler2D _MainTex;
			uniform float4 _Color;

			struct Input {
				float2 uv_MainTex;
			};

			void surf (Input IN, inout SurfaceOutput o) {
				fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
				o.Albedo = c.rgb * _Color.rgb;
				o.Alpha = c.a * _Color.a;
			}
		ENDCG
	}

	Fallback "Mobile/VertexLit"
}
