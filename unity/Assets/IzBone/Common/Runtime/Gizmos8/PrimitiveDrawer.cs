
// OnDrawGizmosで使用できるように、いつでもインクルード可能にしているが、
// Editorでのみ有効である必要があるので、ここで有効無効を切り替える
#if UNITY_EDITOR

using Unity.Mathematics;
using static Unity.Mathematics.math;

using System;
using UnityEditor;
using UnityEngine;

namespace IzBone.Common {
static internal partial class Gizmos8 {


	/** カプセルを表示 */
	public static void drawWireCapsule(float3 pos, float3 upDir, float r_s, float r_h) {
		float3 x,y,z;
		y = upDir;
		x = y.Equals(float3(1,0,0)) ?
			normalize( cross(upDir, float3(0,1,0)) ) :
			normalize( cross(upDir, float3(1,0,0)) );
		z = cross(x, y);

		drawWireCylinder(pos, upDir, r_s, r_h);

		var t =  y*r_h + pos;
		var b = -y*r_h + pos;
		drawWireCircle(t, r_s, float3x3(z,x,y), 0, 0.5f);
		drawWireCircle(t, r_s, float3x3(x,z,y), 0, 0.5f);
		drawWireCircle(b, r_s, float3x3(z,x,y), 0.5f, 1);
		drawWireCircle(b, r_s, float3x3(x,z,y), 0.5f, 1);
	}

	/** 円を描画 */
	public static void drawWireCircle(
		float3 center,
		float r,
		float3x3 rot,
		float from = 0,
		float to = 1
	) {
		int segNum = 30;
		Func<int,float> calcTheta = i => (PI*2)*( from + (to-from)*i/segNum );

		for (int i=0; i<segNum; ++i) {
			var theta0 = calcTheta( i );
			var theta1 = calcTheta( i+1 );
			var p0 = mul( rot, float3(cos(theta0),0,sin(theta0)) ) * r + center;
			var p1 = mul( rot, float3(cos(theta1),0,sin(theta1)) ) * r + center;
			Gizmos.DrawLine(p0, p1);
		}
	}

	/** シリンダーを描画 */
	public static void drawWireCylinder(float3 pos, float3 upDir, float r_s, float r_h) {
		float3 x,y,z;
		y = upDir;
		x = y.Equals(float3(1,0,0)) ?
			normalize( cross(upDir, float3(0,1,0)) ) :
			normalize( cross(upDir, float3(1,0,0)) );
		z = cross(x, y);

		drawWireCircle( pos, r_s, float3x3(x,y,z), 0, 1 );
		var t =  y*r_h + pos;
		var b = -y*r_h + pos;
		drawWireCircle(t, r_s, float3x3(x,y,z), 0, 1);
		drawWireCircle(b, r_s, float3x3(x,y,z), 0, 1);
		Gizmos.DrawLine( b + x*r_s, t + x*r_s );
		Gizmos.DrawLine( b + z*r_s, t + z*r_s );
		Gizmos.DrawLine( b - x*r_s, t - x*r_s );
		Gizmos.DrawLine( b - z*r_s, t - z*r_s );
	}

} }
#endif
