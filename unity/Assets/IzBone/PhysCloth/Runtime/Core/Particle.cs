using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

using System.Runtime.InteropServices;


namespace IzBone.PhysCloth.Core {
	using Common;
	using Common.Field;
	
	/** シミュレート単位となるパーティクル1粒子分の情報 */
	public struct Particle
	{
		// 初期化時のワールド座標と法線。
		// これはアニメーションなどで同期されない真に初期化時の位置
		readonly public float3 initWPos;
		readonly public float3 initWNml;

		// シミュレーションが行われなかった際のL2W。
		// これはシミュレーション対象外のボーンのアニメーションなどを反映して毎フレーム更新する
		public float4x4 defaultL2W;

		// 位置・半径・速度・質量の逆数
		public Common.Collider.Collider_Sphere col;
		public float3 v;
		public float invM;

		// Default位置への復元半減期
		public HalfLife restoreHL;

		public Particle(float3 initWPos, float3 initWNml) {
			this.initWPos = initWPos;
			this.initWNml = initWNml;
			defaultL2W = default;
			col = default;
			col.pos = initWPos;
			v = default;
			invM = default;
			restoreHL = default;
		}

		public void syncParams(float m, float r, HalfLife restoreHL) {
			col.r = r;
			invM = m < MinimumM ? 0 : (1f/m);
			this.restoreHL = restoreHL;
		}

		const float MinimumM = 0.00000001f;
	}

}
