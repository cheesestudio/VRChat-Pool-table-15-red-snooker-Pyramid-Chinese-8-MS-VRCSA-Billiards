Shader "metaphira/TableSurface (Glass)"
{
   Properties
   {
      _Color ("Color", Color) = (1,1,1,1)
      _EmissionColor ("Emission Color", Color) = (1,1,1,1)
      _MainTex ("Albedo (RGB)", 2D) = "white" {}
      _MetalSmooth ("MetalSmooth", 2D) = "white" {}
      _EmissionMap ("EmissionMap", 2D) = "black" {}
      _TimerPct ("Timer Percentage", Range(0, 1)) = 1
   }

   SubShader
   {
      Tags { "Queue"="Transparent" "RenderType"="Transparent" }
      LOD 200
      ZWrite Off
      Blend SrcAlpha OneMinusSrcAlpha

      CGPROGRAM
      #pragma surface surf Standard fullforwardshadows vertex:vert alpha
      #pragma target 3.5

      #include "UnityCG.cginc"

      sampler2D _MainTex;
      sampler2D _EmissionMap;
      sampler2D _MetalSmooth;
      fixed4 _Color;
      fixed4 _EmissionColor;
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

      void surf(Input IN, inout SurfaceOutputStandard o)
      {
         fixed4 color = tex2D(_MainTex, IN.uv_MainTex) * _Color;
         fixed4 emission = tex2D(_EmissionMap, IN.uv_MainTex);
         fixed4 metalSmooth = tex2D(_MetalSmooth, IN.uv_MainTex);
         o.Albedo = color.rgb;
         o.Metallic = metalSmooth.r;
         o.Smoothness = metalSmooth.a;
         o.Alpha = color.a * _Color.a;

         float timerPct = clamp(_TimerPct, 0, 1);
         float surfaceAnglePct = (M_PI + atan2(IN.modelPos.x, IN.modelPos.z)) / (2 * M_PI) / 1.04 + (1 - 1 / 1.04);
         float angle = clamp((surfaceAnglePct - timerPct) * 40.0, 0, 1.5);
         o.Emission = emission.r * _EmissionColor * angle;
      }
      ENDCG
   }

   FallBack "Diffuse"
}
