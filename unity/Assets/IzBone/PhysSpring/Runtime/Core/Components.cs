
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;

namespace IzBone.PhysSpring.Core {
	using Common;

	/** 1繋がりのSpringリストの最親につけるコンポーネント */
	public struct MostParent : IComponentData {
		public int depth;			//!< Springが何個連なっているか
		public int iterationNum;	//!< 繰り返し計算回数
		public Entity colliderPack;	//!< 衝突検出を行う対象のコライダー

		/**
		 * 回転と移動の影響割合。
		 * 0だと回転のみ。1だと移動のみ。その間はブレンドされる。
		 */
		public float rsRate;

		public float4x4 ppL2W;	//!< ボーン親の更に親のL2W
	}

	/**
	 * シミュレーションの1間接部分の情報を表すコンポーネント。
	 * 間接ごとのEntityに対して付ける。
	 */
	public struct OneSpring : IComponentData {
		public Math8.SmoothRange_Float range_rot;		//!< 回転 - 範囲情報
		public Math8.SmoothRange_Float3 range_sft;		//!< 移動 - 範囲情報
		public Math8.Spring_Float3 spring_rot;			//!< 回転 - バネ
		public Math8.Spring_Float3 spring_sft;			//!< 移動 - バネ
	}

	/** OneSpringごとのデフォルト位置姿勢情報 */
	public struct DefaultState : IComponentData {
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

	/** OneSpringごとのシミュレーション結果の情報 */
	public struct SpringResult : IComponentData {
		public float3 trs;			//!< 計算結果のローカル座標
		public quaternion rot;		//!< 計算結果のローカル姿勢
	}

	/** OneSpringごとのワールド座標のキャッシュ */
	public struct WPosCache : IComponentData {
		public float3 lastWPos;		//!< 前フレームでのワールド位置のキャッシュ
	}

	/** Springの子供側のEntityを得る */
	public struct Child : IComponentData {
		public Entity value;
	}

	/** OneSpringとRootAuthoringとの橋渡し役を行うためのマネージドコンポーネント */
	public sealed class OneSpring_M2D : IComponentData {
		public RootAuthoring.Bone boneAuth;			//!< 生成元
		public Transform parentTrans, childTrans;	//!< Springでつながる親と子のTransform
		public float depthRate;						//!< 0~Depthを0～1にリマップしたもの
	}
}
