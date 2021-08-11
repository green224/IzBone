using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

using System.Runtime.InteropServices;


namespace IzBone.PhysCloth.Core {
	using Common;
	using Common.Field;
	
	/** シミュレート単位となるパーティクル1粒子分の情報 */
	public struct Particle {
		public Common.Collider.Collider_Sphere col;
		public float3 v;
		public float invM;
		public float4x4 defaultL2W;
		public HalfLife restoreHL;		// 初期位置への復元半減期


		public void syncParams(float m, float r, HalfLife restoreHL) {
			col.r = r;
			invM = m < MinimumM ? 0 : (1f/m);
			this.restoreHL = restoreHL;
		}

		const float MinimumM = 0.00000001f;
	}

}
