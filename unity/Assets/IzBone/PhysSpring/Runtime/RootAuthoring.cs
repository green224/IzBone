using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;


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
		[RangeSC(0,180)] public SC angleMax = 60;
		[RangeSC(0,1)] public SC angleMargin = 0.3f;
		[RangeSC(0,100)] public SC omgMax = 20;
		[RangeSC(0,1000)] public SC rotKpm = 70;
		[RangeSC(0,1)] public SC omgDrag = 0.5f;

		[Space]
		[RangeSC(0)] public SC shiftMax = 1;
		[RangeSC(0,1)] public SC shiftMargin = 0.3f;
		[RangeSC(0)] public SC vMax = 1;
		[RangeSC(0)] public SC shiftKpm = 1000;
		[RangeSC(0,1)] public SC vDrag = 0.5f;

		[Space]
		[RangeSC(0)] public SC radius = 0.1f;			//!< パーティクル半径
		[JointCount(1)] public int depth = 1;			//!< ボーン深度
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

	void OnEnable()
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
	void OnValidate() {
		if (_bones == null) return;
		foreach ( var i in _bones ) {
			// Depthを有効範囲に丸める
			int depthMax = 0;
			foreach (var t in i.targets) {
				int s = 0;
				for (var j=t.endOfBone; j.parent!=null; j=j.parent) ++s;
				depthMax = max(s, depthMax);
			}

			i.depth = Mathf.Clamp(i.depth, 1, depthMax);
		}
	}
#endif
}
}
