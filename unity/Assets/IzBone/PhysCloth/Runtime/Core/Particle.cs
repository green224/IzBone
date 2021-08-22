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

		// 初期化時のワールド座標と法線。
		// これはアニメーションなどで同期されない真に初期化時の位置
		readonly public float3 initWPos;
		readonly public float3 initWNml;
		readonly public float3 initLNml;

		// シミュレーションが行われなかった際のL2W。
		// これはシミュレーション対象外のボーンのアニメーションなどを反映して毎フレーム更新する
		public float4x4 defaultL2W;
		public float3 defaultWNml;

		// 位置・半径・速度・質量の逆数
		public Common.Collider.Collider_Sphere col;
		public float3 v;
		public float invM;

		// 現在の姿勢値。デフォルト姿勢からの差分値。ワールド座標で計算する
		public quaternion dWRot;

		// 現在のワールド法線
		public float3 wNml;

		// 最大差分角度(ラジアン)
		public float maxDRotAngle;

		// 角度変位の拘束条件へのコンプライアンス値
		public float angleCompliance;

		// Default位置への復元半減期
		public HalfLife restoreHL;


		public Particle(
			int index,
			float3 initWPos, float3 initWNml, float3 initLNml,
			float angleCompliance
		) {
			this.index = index;
			this.initWPos = initWPos;
			this.initWNml = initWNml;
			this.initLNml = initLNml;

			defaultL2W = default;
			defaultWNml = default;
			col = default;
			col.pos = initWPos;
			v = default;
			invM = default;
			dWRot = Unity.Mathematics.quaternion.identity;
			wNml = default;
			maxDRotAngle = default;
			restoreHL = default;
			this.angleCompliance = angleCompliance;
		}

		public void syncParams(
			float m, float r,
			float maxDRotAngle,
			float angleCompliance,
			HalfLife restoreHL
		) {
			col.r = r;
			invM = m < MinimumM ? 0 : (1f/m);
			this.maxDRotAngle = maxDRotAngle;
			this.angleCompliance = angleCompliance;
			this.restoreHL = restoreHL;
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
