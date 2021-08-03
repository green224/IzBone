using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace IzBone.PhysSpring {
using Common;

/**
 * おっぱいやしっぽ、短いアクセサリーなどの単純なゆれものを
 * シンプルなシミュレーションで再現する際に使用するモジュール。
 *
 * 重力は考慮せず、正確な物理近似ではないのでボーン数が少ない方がよい見た目になる事が多い。
 * 必要であればアニメーション付きのボーンに、ゆれを付加することもできる。
 */
[AddComponentMenu("IzBone/PhysSpring")]
[UnityEngine.Animations.NotKeyable]
[DisallowMultipleComponent]
public sealed class RootAuthoring : MonoBehaviour {
	// --------------------------- インスペクタに公開しているフィールド -----------------------------

	[Serializable] public sealed class Bone {

		public string name = "name";		//!< 名前。これはEditor表示用なので特別な意味はない

		[Serializable] public sealed class OneTrnasParam {			//!< 目標の1Transformごとのパラメータ
			public Transform endOfBone = null;									//!< 末端Transform
			[UseEuler] public Quaternion defaultRot = Quaternion.identity;		//!< 根元の初期姿勢
		}
		[Space]
		public OneTrnasParam[] targets = null;			//!< 目標のTransformたち

		[Space]
		[Range(0,180)]	public float angleMax = 60;		//!< 姿勢の差分角度の最大値
		[Range(0,90)]	public float angleMargin = 4;	//!< 姿勢の差分角度の最大値付近のスムースマージン
		[Range(0,100)]	public float omgMax = 20;		//!< 角速度の最大値
		[Range(1,1000)]	public float rotKpm = 70;		//!< バネ係数/質量
		[Range(0.001f,1)] public float omgHL = 0.1f;	//!< 空気抵抗による速度半減期

		[Space]
		[Min(0)] public float shiftMax = 1;				//!< 位置の差分距離の最大値
		[Min(0)] public float shiftMargin = 0.1f;		//!< 位置の差分距離の最大値付近のスムースマージン範囲
		[Min(0)] public float vMax = 1;					//!< 速度の最大値
		[Min(0)] public float shiftKpm = 1000;			//!< バネ係数/質量
		[Range(0.001f,1)] public float vHL = 0.1f;		//!< 空気抵抗による速度半減期。移動に関するもの。これを小さくしておくと収束しやすい

		[Space]
		[Range(1,10)] public int depth = 1;				//!< ボーン深度
		[Range(1,10)] public int iterationNum = 1;		//!< 繰り返し計算回数

		[Range(0,1)] public float rotShiftRate = 0.5f;	//!< 回転と移動の反映割合
		public bool withAnimation = false;				//!< アニメーション付きのボーンに対して使用するフラグ。毎フレームデフォルト位置を再キャッシュする。
	}

	[SerializeField] internal Bone[] _bones = new []{new Bone()};


	// ------------------------------------- public メンバ ----------------------------------------

	/** 物理状態をリセットする */
	[ContextMenu("reset")]
	public void reset() {
		GetSys().reset(_erRegLink);
	}


	// --------------------------------- private / protected メンバ -------------------------------

	/** ECSで得た結果をマネージドTransformに反映するためのバッファのリンク情報。System側から設定・参照される */
	Core.EntityRegisterer.RegLink _erRegLink = new Core.EntityRegisterer.RegLink();

	/** メインのシステムを取得する */
	Core.SimplePhysBoneSystem GetSys() {
		var w = World.DefaultGameObjectInjectionWorld;
		if (w == null) return null;
		return w.GetOrCreateSystem<Core.SimplePhysBoneSystem>();
	}

	private void OnEnable()
	{
		var sys = GetSys();
		if (sys != null) sys.register(this, _erRegLink);
	}

	void OnDisable()
	{
		var sys = GetSys();
		if (sys != null) sys.unregister(this, _erRegLink);
	}


	// --------------------------------------------------------------------------------------------
#if UNITY_EDITOR

	void OnDrawGizmos() {

		// 自分が含まれているツリーを選択中のみ表示する
		if (!Gizmos8.isSelectedTransformTree(Selection.objects, transform)) return;

		foreach (var bone in _bones)
		foreach (var boneTgt in bone.targets) {

			// 間接の位置リストを構築
			var posLst = new float3[bone.depth + 1];
			var trns = boneTgt.endOfBone;
			posLst[0] = trns.position;
			for (int i=0; i<bone.depth; ++i) {
				var next = trns.parent;
				posLst[i+1] = next.position;
				trns = next;

				// ついでに角度範囲を描画
				var l2w = (float4x4)next.localToWorldMatrix;
				if (bone.rotShiftRate < 0.9999f) {
					var pos = l2w.c3.xyz;
					var rot = next.rotation;
					var scl = HandleUtility.GetHandleSize(l2w.c3.xyz)/2;
					Gizmos.color = Color.blue;
					Gizmos8.drawAngleCone( pos, rot, scl, bone.angleMax+bone.angleMargin );
					Gizmos.color = Color.white;
					Gizmos8.drawAngleCone( pos, rot, scl, bone.angleMax );
				}

				// ついでに移動可能範囲を描画
				if (0.00001f < bone.rotShiftRate) {
					var scl0 = Unity.Mathematics.float4x4.TRS(
						0, Unity.Mathematics.quaternion.identity, bone.shiftMax
					);
					var scl1 = Unity.Mathematics.float4x4.TRS(
						0, Unity.Mathematics.quaternion.identity, bone.shiftMax + bone.shiftMargin
					);
					Gizmos.color = Color.white;
					Gizmos8.drawWireCube( mul(l2w, scl0) );
					Gizmos.color = Color.blue;
					Gizmos8.drawWireCube( mul(l2w, scl1) );
				}
			}

			// 描画
			Gizmos8.drawBones(posLst);
		}
	}

//	void OnValidate() {
//		foreach (var bone in _bones) {
//			// 移動差分距離の限界値等を許容値範囲にクランプする
//			bone.shiftMax = max(0, bone.shiftMax);
//			bone.shiftMargin = max(0, bone.shiftMargin);
//			bone.vMax = max(0, bone.vMax);
//			bone.shiftKpm = max(0, bone.shiftKpm);
//		}
//	}
#endif
}
}
