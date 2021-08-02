using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;



namespace IzPhysBone.Math8 {

/** 数学関数群 */
static class Math8
{
	// --------------------------------------- publicメンバ -------------------------------------

	/** 指定のfrom方向をto方向に向ける回転を得る */
	static public quaternion FromToRotation(float3 from,　float3 to) {
		var axis = normalizesafe( cross(from, to) );
		var theta = acos( clamp( dot(normalizesafe(from), normalizesafe(to)), -1, 1 ) );
		var s = sin(theta / 2);
		var c = cos(theta / 2);
		return quaternion(axis.x*s, axis.y*s, axis.z*s, c);
	}


	// ----------------------------------- private/protected メンバ -------------------------------
	// --------------------------------------------------------------------------------------------
}

}

