using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

using System.Runtime.InteropServices;


namespace IzBone.PhysCloth.Core {
	using Common;
	using Common.Field;
	
	/** シミュレート単位となるパーティクル1粒子分の情報 */
	public unsafe struct Particle
	{
		readonly public int index;
		readonly public int parentIdx;

		// シミュレーションが行われなかった際のL2W。
		// これはシミュレーション対象外のボーンのアニメーションなどを反映して毎フレーム更新する
		public float4x4 defaultHeadL2W;
		public float3 defaultTailWPos;

		// 位置・半径・速度・質量の逆数
		public IzBCollider.RawCollider.Sphere col;
		public float3 v;
		public float invM;

		// 現在の姿勢値。デフォルト姿勢からの差分値。ワールド座標で計算する
		public quaternion dWRot;

		// 最大差分角度(ラジアン)
		public float maxDRotAngle;

		// 角度変位の拘束条件へのコンプライアンス値
		public float angleCompliance;

		// Default位置への復元半減期
		public HalfLife restoreHL;

		// デフォルト位置からの移動可能距離
		public float maxMovableRange;



		public Particle(
			int index,
			int parentIdx,
			float3 initWPos
		) {
			this.index = index;
			this.parentIdx = parentIdx;

			defaultHeadL2W = default;
			defaultTailWPos = default;
			col = default;
			col.pos = initWPos;
			v = default;
			invM = default;
			dWRot = Unity.Mathematics.quaternion.identity;
			maxDRotAngle = default;
			restoreHL = default;
			angleCompliance = default;
			maxMovableRange = default;
		}

		public void syncParams(
			float m, float r,
			float maxDRotAngle,
			float angleCompliance,
			HalfLife restoreHL,
			float maxMovableRange
		) {
			col.r = r;
			invM = m < MinimumM ? 0 : (1f/m);
			this.maxDRotAngle = maxDRotAngle;
			this.angleCompliance = angleCompliance;
			this.restoreHL = restoreHL;
			this.maxMovableRange = maxMovableRange;
		}

		const float MinimumM = 0.00000001f;
	}

	/** ひとつなぎのボーンに対応するパーティクル一式を扱う情報 */
	public unsafe struct ParticleChain
	{
		readonly public Particle* begin;
		readonly public int length;

		public ParticleChain(Particle* begin, int length) {
			this.begin = begin;
			this.length = length;
		}

		public Particle this[int index] {
			get {
				Unity.Assertions.Assert.IsTrue(0<=index && index<length);
				return begin[index];
			}
			set {
				Unity.Assertions.Assert.IsTrue(0<=index && index<length);
				begin[index] = value;
			}
		}
	}

}
