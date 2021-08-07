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
using Common.Field;

using RangeSC = Common.Field.SimpleCurveRangeAttribute;
using SC = Common.Field.SimpleCurve;

/**
 * おっぱいやしっぽ、短いアクセサリーなどの単純なゆれものを
 * シンプルなシミュレーションで再現する際に使用するモジュール。
 *
 * 重力は考慮せず、正確な物理近似ではないのでボーン数が少ない方がよい見た目になる事が多い。
 * 必要であればアニメーション付きのボーンに、ゆれを付加することもできる。
 */
[AddComponentMenu("IzBone/PhysSpring")]
//[UnityEngine.Animations.NotKeyable]
//[DisallowMultipleComponent]
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
		[UnityEngine.Serialization.FormerlySerializedAs("angleMax3")]
		[RangeSC(0,180)] public SC angleMax = 60;
		[UnityEngine.Serialization.FormerlySerializedAs("angleMargin3")]
		[RangeSC(0,1)] public SC angleMargin = 0.3f;
		[UnityEngine.Serialization.FormerlySerializedAs("omgMax3")]
		[RangeSC(0,100)] public SC omgMax = 20;
		[UnityEngine.Serialization.FormerlySerializedAs("rotKpm3")]
		[RangeSC(0,1000)] public SC rotKpm = 70;
		[UnityEngine.Serialization.FormerlySerializedAs("omgHL3")]
		[RangeSC(0.001f,1)] public SC omgHL = 0.1f;

		[Space]
		[UnityEngine.Serialization.FormerlySerializedAs("shiftMax3")]
		[RangeSC(0)] public SC shiftMax = 1;
		[UnityEngine.Serialization.FormerlySerializedAs("shiftMargin3")]
		[RangeSC(0,1)] public SC shiftMargin = 0.3f;
		[UnityEngine.Serialization.FormerlySerializedAs("vMax3")]
		[RangeSC(0)] public SC vMax = 1;
		[UnityEngine.Serialization.FormerlySerializedAs("shiftKpm3")]
		[RangeSC(0)] public SC shiftKpm = 1000;
		[UnityEngine.Serialization.FormerlySerializedAs("vHL3")]
		[RangeSC(0.001f,1)] public SC vHL = 0.1f;

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
		Gizmos8.drawMode = Gizmos8.DrawMode.Handle;

		foreach (var bone in _bones)
		foreach (var boneTgt in bone.targets) {

			var trns = boneTgt.endOfBone;
			if (trns == null) continue;

			// 間接の位置リストを構築
			var posLst = new float3[bone.depth + 1];
			posLst[0] = trns.position;
			for (int i=0; i<bone.depth; ++i) {
				var next = trns.parent;
				posLst[i+1] = next.position;
				var iRate = (float)(bone.depth-1-i) / max(bone.depth-1, 1);

				// ついでに角度範囲を描画
				var l2w = (float4x4)next.localToWorldMatrix;
				if (bone.rotShiftRate < 0.9999f) {
					var pos = l2w.c3.xyz;
					var rot = mul(
						Math8.fromToRotation(
							mul( next.rotation, float3(0,1,0) ),
							trns.position - next.position
						),
						next.rotation
					);
//					var scl = HandleUtility.GetHandleSize(l2w.c3.xyz)/2;
					var scl = length(trns.position - next.position)/2;
					Gizmos8.color = new Color(0.97f,0.7f,0);
					var agl = bone.angleMax.evaluate(iRate);
					Gizmos8.drawAngleCone( pos, rot, scl, agl );
					Gizmos8.color = new Color(1,0,0);
					agl *= 1 - bone.angleMargin.evaluate(iRate);
					Gizmos8.drawAngleCone( pos, rot, scl, agl );
				}

				// ついでに移動可能範囲を描画
				if (0.00001f < bone.rotShiftRate) {
					var sft = bone.shiftMax.evaluate(iRate);
					var scl1 = Unity.Mathematics.float4x4.TRS(
						0, Unity.Mathematics.quaternion.identity, sft
					);
					sft *= 1 - bone.shiftMargin.evaluate(iRate);
					var scl0 = Unity.Mathematics.float4x4.TRS(
						0, Unity.Mathematics.quaternion.identity, sft
					);
					Gizmos8.color = new Color(0,0.7f,0.97f);
					Gizmos8.drawWireCube( mul(l2w, scl1) );
					Gizmos8.color = new Color(0,0,1);
					Gizmos8.drawWireCube( mul(l2w, scl0) );
				}

				trns = next;
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
