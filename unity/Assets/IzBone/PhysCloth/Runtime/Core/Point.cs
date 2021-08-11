using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

using System.Runtime.InteropServices;


namespace IzBone.PhysCloth.Core {
	
	/** シミュレート単位となるパーティクル1粒子分の情報 */
	public struct Point {
		public Common.Collider.Collider_Sphere col;
		public float3 v;
		public float invM;
		public float4x4 defaultL2W;

		public Point(float3 pos, float m, float r) {
			col.pos = pos;
			col.r = r;
			v = default;
			invM = m < MinimumM ? 0 : (1f/m);
			defaultL2W = default;
		}

		const float MinimumM = 0.00000001f;
	}

}
