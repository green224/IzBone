
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using System.Runtime.InteropServices;

namespace IzBone.PhysCloth.Core {
	using Common;
	using Common.Field;

	/** PhysCloth１セット分の、Particleの最親につけるコンポーネント */
	public struct OneCloth : IComponentData {
	}



	// 以降、１ParticleごとのEntityに対して付けるコンポーネント群

	public struct Ptcl:IComponentData {}

	// PhysCloth１セット内の、次のPaticle・親のParticleへの参照。
	public struct Ptcl_Next:IComponentData {public Entity value;}
	public struct Ptcl_Parent:IComponentData {public Entity value;}

	// シミュレーションが行われなかった際のL2W,L2P,親の初期姿勢。
	// L2Wはシミュレーション対象外のボーンのアニメーションなどを反映して毎フレーム更新する
	public struct Ptcl_DefaultL2W:IComponentData {public float4x4 value;}
	public struct Ptcl_DefaultL2P:IComponentData {public float4x4 value;}
	public struct Ptcl_DefaultParentRot:IComponentData {public quaternion value;}

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



	// 以降、１DistanceConstraintごとのEntityに対して付けるコンポーネント群

	public struct DistCstr:IComponentData {}
	public struct Cstr_Target:IComponentData {public Entity src, dst;}		// 処理対象のParticle
	public struct Cstr_Compliance:IComponentData {public float value;}		// コンプライアンス値
	public struct Cstr_DefaultLen:IComponentData {public float value;}		// 初期長さ



	// ECSとAuthoringとの橋渡し役を行うためのマネージドコンポーネント
	public sealed class Ptcl_M2D:IComponentData {
		public Authoring.ParticleMng auth;				//!< 生成元
	}
	public sealed class Cstr_M2D:IComponentData {
		public Authoring.ConstraintMng auth;			//!< 生成元
	}

}
