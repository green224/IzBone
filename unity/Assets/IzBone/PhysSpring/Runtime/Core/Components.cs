
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace IzBone.PhysSpring.Core {
	using Common;
	using ICD = IComponentData;

	// 以降、1繋がりのSpringリスト１セットごとのEntityに対して付けるコンポーネント群

	// 1繋がりのSpringリストを管理するコンポーネント。
	// 再親のEntityについているわけではない事に注意
	public struct Root:ICD {
		public int depth;			//!< Springが何個連なっているか
		public int iterationNum;	//!< 繰り返し計算回数
		public Entity colliderPack;	//!< 衝突検出を行う対象のコライダー

		/**
		 * 回転と移動の影響割合。
		 * 0だと回転のみ。1だと移動のみ。その間はブレンドされる。
		 */
		public float rsRate;

		public float4x4 rootL2W;	//!< ボーン親の更に親のL2W
		public float4x4 rootW2L;	//!< ボーン親の更に親のW2L

		public Entity firstPtcl;	// Springの開始位置のEntity。Ptclがついている
	}



	// 以降、１ParticleごとのEntityに対して付けるコンポーネント群

	/**
	 * シミュレーションの1間接部分の情報を表すコンポーネント。
	 * 間接ごとのEntityに対して付ける。
	 */
	public struct OneSpring:ICD {
		public Math8.SmoothRange_Float range_rot;		//!< 回転 - 範囲情報
		public Math8.SmoothRange_Float3 range_sft;		//!< 移動 - 範囲情報
		public Math8.Spring_Float3 spring_rot;			//!< 回転 - バネ
		public Math8.Spring_Float3 spring_sft;			//!< 移動 - バネ
	}

	/** OneSpringごとのデフォルト位置姿勢情報 */
	public struct DefaultState:ICD {
		/**
		 * 毎フレーム現在位置をデフォルト位置として初期化するか否か。
		 * しっぽなどのアニメーションを含むボーンに対して後付けで使用する際には、
		 * これを有効にして毎フレームデフォルト位置を更新する。
		 */
		public bool resetDefPosAlways;

		public quaternion defRot;		//!< 親の初期姿勢
		public float3 defPos;			//!< 親の初期ローカル座標
		public float3 childDefPos;		//!< 子の初期ローカル座標
		public float3 childDefPosMPR;	//!< 子の初期ローカル座標に親の回転とスケールを掛けたもの。これはキャッシュすべきか悩みどころ…

		public float3 curScale;		//!< 現在のローカルスケール。これは毎フレームTransformからコピーする

		public float r;				//!< 衝突判定用の半径
	}

	public struct Ptcl_LastWPos:ICD {public float3 value;}	// 前フレームでのワールド位置のキャッシュ
	public struct Ptcl_Child:ICD {public Entity value;}		// 子供側のEntity




	// シミュレーション毎のTransformの現在値の取得と、
	// シミュレーション結果をフィードバックする際に使用されるTransform情報。
	// これは一番末端PtclやRootにも付くが、そいつは参照用のみに使用される。（フィードバックもされてしまうが）
	public struct CurTrans:ICD {
		public float3 lPos;
		public quaternion lRot;
	}




	// ECSとAuthoringとの橋渡し役を行うためのマネージドコンポーネント
	public sealed class OneSpring_M2D:ICD {
		public RootAuthoring.Bone boneAuth;			// 生成元
		public Transform parentTrans, childTrans;	// Springでつながる親と子のTransform
		public float depthRate;						// 0~Depthを0～1にリマップしたもの
	}
	public sealed class Root_M2D:ICD {
		public RootAuthoring.Bone auth;			// 生成元
	}
}
