// MIT License

// Copyright (c) 2017 Xavier Martinez

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

Shader "GeoAO/VertAOOpti" {
	//Example shader to show how to use computed AO value
	
	Properties {
		_MainTex ("Base (RGB)", 2D) = "white" {}
		_AOColor ("AO Color", Color) = (0,0,0,1)
		_AOScale ("AO Scale", Range(0.0, 5.0)) = 1.0
		_AOTex ("AO Texture", 2D) = "white" {}
	}
	
	SubShader {
		
		Tags {"RenderType" = "Opaque"}
		
		LOD 200

			CGPROGRAM
			

			#pragma surface surf Lambert vertex:vert

			sampler2D _AOTex;
			float4 _AOTex_TexelSize;

			sampler2D _MainTex;
			half4 _AOColor;
			float _AOScale;

			struct Input {
				float2 uv_MainTex : TEXCOORD0;
				float aoVal : TEXCOORD1;
				float4 color      : COLOR;
			};


			void vert (inout appdata_full v, out Input o){
		          UNITY_INITIALIZE_OUTPUT(Input,o);
		          o.aoVal = tex2Dlod(_AOTex,float4(v.texcoord1.x,v.texcoord1.y,0,0) ).a;
		      }

			void surf (Input IN, inout SurfaceOutput o) {
				half4 c = tex2D (_MainTex, IN.uv_MainTex);
				half ao = (1-IN.aoVal)*_AOScale;

				o.Albedo = lerp(c.rgb, _AOColor, ao);
				o.Alpha = c.a;
			}
			
			ENDCG
		}
	FallBack "Diffuse"
	
}
