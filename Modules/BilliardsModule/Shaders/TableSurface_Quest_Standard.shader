Shader "metaphira/TableSurface (Quest)"
{
   Properties
   {
      _EmissionColor ("Emission Colour", Color) = (1,1,1,1)
      _Color ("Tint Colour", Color) = (1,1,1,1)
      _MainTex ("Albedo (RGB)", 2D) = "white" {}
      _EmissionMap ("Emission mask", 2D) = "black" {}
      _TimerPct ("Timer Percentage", Range(0, 1)) = 1
   }

   SubShader
   {
      Tags { "RenderType"="Opaque" }
      LOD 200

      CGPROGRAM
      #pragma surface surf Lambert noforwardadd vertex:vert
      #pragma target 3.5

      #include "UnityCG.cginc"

      sampler2D _MainTex;
      sampler2D _EmissionMap;
      fixed4 _EmissionColor;
      fixed3 _Color;
      float _TimerPct;

      struct Input
      {
         float2 uv_MainTex;
         float3 modelPos;
      };

      void vert(inout appdata_full v, out Input o)
      {
         UNITY_INITIALIZE_OUTPUT(Input, o);
         o.modelPos = v.vertex.xyz;
      }

      static const float M_PI = 3.14159265358979323846264338327950288;

      void surf(Input IN, inout SurfaceOutput o)
      {
         fixed4 diffuse = tex2D(_MainTex, IN.uv_MainTex);
         fixed4 emission = tex2D(_EmissionMap, IN.uv_MainTex);
         o.Albedo = lerp(diffuse.rgb, _Color * diffuse.rgb * 2.0, pow(diffuse.a, 0.1));

         float timerPct = clamp(_TimerPct, 0, 1);
         float surfaceAnglePct = (M_PI + atan2(IN.modelPos.x, IN.modelPos.z)) / (2 * M_PI) / 1.04 + (1 - 1 / 1.04);
         float angle = clamp((surfaceAnglePct - timerPct) * 40.0, 0, 1.5);
         o.Emission = emission.r * _EmissionColor * angle;
      }
      ENDCG
   }

   FallBack "Diffuse"
}
