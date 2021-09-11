
using UnityEngine;
using Unity.Entities;
using UnityEngine.Jobs;
using System.Runtime.InteropServices;
using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysCloth.Core {
	using Common;
	using Common.Field;

	// 以降、PhysCloth１セットごとのEntityに対して付けるコンポーネント群

	public struct Root : IComponentData {}
	public struct Root_UseSimulation : IComponentData {public bool value;}	// 物理演算を行うか否か
	public struct Root_G : IComponentData {public float3 value;}			// 重力加速度
	public struct Root_Air : IComponentData {		// 空気関連のパラメータ
		public float3 winSpd;				// 風速
		public HalfLife airDrag;			// 空気抵抗

		public float3 winSpdIntegral;		// 空気抵抗を考慮して積分した結果
		public float airResRateIntegral;	// 空気抵抗の積分結果
	}
	public struct Root_MaxSpd : IComponentData {public float value;}		// 最大速度
	public struct Root_WithAnimation : IComponentData {public bool value;}	// 毎フレームデフォルト位置を再キャッシュする
	public struct Root_ColliderPack : IComponentData {public Entity value;}	// 衝突検出を行う対象のコライダー



	// 以降、１ParticleごとのEntityに対して付けるコンポーネント群

	public struct Ptcl:IComponentData {}

	// PhysCloth１セット内の、次のPaticle・親のParticle・RootのParticleへの参照。
	public struct Ptcl_Next:IComponentData {public Entity value;}
	public struct Ptcl_Parent:IComponentData {public Entity value;}
	public struct Ptcl_Root:IComponentData {public Entity value;}

	// シミュレーションが行われなかった際のL2W,L2P。
	// L2Wはシミュレーション対象外のボーンのアニメーションなどを反映して毎フレーム更新する
	public struct Ptcl_DefaultHeadL2W:IComponentData {public float4x4 value;}
	public struct Ptcl_DefaultHeadL2P:IComponentData {
		public float4x4 l2p;
		public quaternion rot;
		public Ptcl_DefaultHeadL2P(Transform trans) {
			if (trans == null) {
				rot = Unity.Mathematics.quaternion.identity;
				l2p = Unity.Mathematics.float4x4.identity;
			} else {
				rot = trans.localRotation;
				l2p = Unity.Mathematics.float4x4.TRS(
					trans.localPosition,
					trans.localRotation,
					trans.localScale
				);
			}
		}
		public Ptcl_DefaultHeadL2P(TransformAccess trans) {
			rot = trans.localRotation;
			l2p = Unity.Mathematics.float4x4.TRS(
				trans.localPosition,
				trans.localRotation,
				trans.localScale
			);
		}
	}
	public struct Ptcl_DefaultTailLPos:IComponentData {public float3 value;}
	public struct Ptcl_DefaultTailWPos:IComponentData {public float3 value;}

	// 位置・半径・速度・質量の逆数
	public struct Ptcl_Sphere:IComponentData {public IzBCollider.RawCollider.Sphere value;}
	public struct Ptcl_V:IComponentData {public float3 value;}
	public struct Ptcl_InvM:IComponentData {
		readonly public float value;
		public const float MinimumM = 0.00000001f;
		public Ptcl_InvM(float m) { value = m < MinimumM ? 0 : (1f/m); }
	}

	// 現在の姿勢値。デフォルト姿勢からの差分値。ワールド座標で計算する
	public struct Ptcl_DWRot:IComponentData {public quaternion value;}

	// 最大差分角度(ラジアン)
	public struct Ptcl_MaxAngle:IComponentData {public float value;}
	// 角度変位の拘束条件へのコンプライアンス値
	public struct Ptcl_AngleCompliance:IComponentData {public float value;}

	// Default位置への復元半減期
	public struct Ptcl_RestoreHL:IComponentData {public HalfLife value;}
	// デフォルト位置からの移動可能距離
	public struct Ptcl_MaxMovableRange:IComponentData {public float value;}

	// λ
	public struct Ptcl_CldCstLmd:IComponentData {public float value;}
	public struct Ptcl_AglLmtLmd:IComponentData {public float value;}
	public struct Ptcl_MvblRngLmd:IComponentData {public float value;}

	// シミュレーション結果をフィードバックする用のTransform情報
	public struct Ptcl_CurHeadTrans:IComponentData {
		public float4x4 l2w;
		public float4x4 w2l;
		public float3 lPos;
		public quaternion lRot;
	}
	



	// 以降、１DistanceConstraintごとのEntityに対して付けるコンポーネント群

	public struct DistCstr:IComponentData {}
	public struct Cstr_Target:IComponentData {public Entity src, dst;}		// 処理対象のParticle
	public struct Cstr_Compliance:IComponentData {public float value;}		// コンプライアンス値
	public struct Cstr_DefaultLen:IComponentData {public float value;}		// 初期長さ
	public struct Cstr_Lmd:IComponentData {public float value;}				// λ



	// ECSとAuthoringとの橋渡し役を行うためのマネージドコンポーネント
	public sealed class Root_M2D:IComponentData {
		public Authoring.BaseAuthoring auth;			//!< 生成元
	}
	public sealed class Ptcl_M2D:IComponentData {
		public Authoring.ParticleMng auth;				//!< 生成元
	}
	public sealed class Cstr_M2D:IComponentData {
		public Authoring.ConstraintMng auth;			//!< 生成元
	}

}
