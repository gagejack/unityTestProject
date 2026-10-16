Shader "StarterGame/White Block Grid"
{
 Properties
 {
  _BaseColor("Block Color", Color) = (0.18,0.22,0.27,1)
  _LineColor("Stroke Color", Color) = (1,1,1,1)
  _GridSize("Block Size (meters)", Float) = 1
  _LineWidth("Stroke Width (meters)", Float) = 0.025
  [HideInInspector] _BaseMap("Base Map", 2D) = "white" {}
  [HideInInspector] _Cutoff("Cutoff", Float) = 0.5
  [HideInInspector] _Cull("Cull", Float) = 2
 }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" }
  Pass
  {
   Name "GridForward"
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   CBUFFER_START(UnityPerMaterial)
   half4 _BaseColor;
   half4 _LineColor;
   float _GridSize;
   float _LineWidth;
   float4 _BaseMap_ST;
   float _Cutoff;
   CBUFFER_END
   struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
   struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float3 metric:TEXCOORD2; float3 normalOS:TEXCOORD3; float fog:TEXCOORD4; };
   Varyings Vert(Attributes v)
   {
    Varyings o;
    VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
    o.positionCS=p.positionCS; o.positionWS=p.positionWS;
    o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.normalOS=v.normalOS;
    // Object-anchored coordinates keep the pattern attached to moving limbs.
    float3 scale=float3(length(TransformObjectToWorldDir(float3(1,0,0),false)),length(TransformObjectToWorldDir(float3(0,1,0),false)),length(TransformObjectToWorldDir(float3(0,0,1),false)));
    o.metric=(v.positionOS.xyz+0.5)*scale;
    o.fog=ComputeFogFactor(p.positionCS.z); return o;
   }
   half4 Frag(Varyings i):SV_Target
   {
    float3 n=abs(i.normalOS);
    float2 uv=n.y>=n.x && n.y>=n.z ? i.metric.xz : (n.x>=n.z ? i.metric.zy : i.metric.xy);
    float spacing=max(_GridSize,0.01);
    float2 cell=uv/spacing;
    float2 edge=abs(frac(cell+0.5)-0.5)*spacing;
    float2 aa=max(fwidth(uv),0.0001);
    float2 stroke=1-smoothstep(_LineWidth*0.5-aa,_LineWidth*0.5+aa,edge);
    float gridMask=max(stroke.x,stroke.y);
    // Fade sub-pixel grids in the distance to reduce shimmer.
    gridMask*=saturate(spacing/(max(aa.x,aa.y)*3));
    Light main=GetMainLight(TransformWorldToShadowCoord(i.positionWS));
    half3 normal=normalize(i.normalWS);
    half3 lighting=max(SampleSH(normal),half3(0.18,0.18,0.18))+main.color*saturate(dot(normal,main.direction))*main.shadowAttenuation;
    half3 color=lerp(_BaseColor.rgb,_LineColor.rgb,gridMask)*lighting;
    color+=_LineColor.rgb*gridMask*0.25;
    return half4(MixFog(color,i.fog),1);
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
  UsePass "Universal Render Pipeline/Lit/DepthNormals"
 }
}

