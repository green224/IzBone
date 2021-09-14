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
[AddComponentMenu("IzBone/IzBone_PhysSpring")]
//[UnityEngine.Animations.NotKeyable]
//[DisallowMultipleComponent]
public sealed class RootAuthoring : MonoBehaviour {
	// --------------------------- インスペクタに公開しているフィールド -----------------------------

	[Serializable] public sealed class Bone {

		public string name = "name";		// 名前。これはEditor表示用なので特別な意味はない

		[Serializable] public sealed class OneTrnasParam {			// 目標の1Transformごとのパラメータ
			public Transform topOfBone = null;									// 再親のTransform
			[UseEuler] public Quaternion defaultRot = Quaternion.identity;		// 根元の初期姿勢
		}
		[Space]
		public OneTrnasParam[] targets = null;			// 目標のTransformたち

		[Space]
		[RangeSC(0,180)] public SC angleMax = 60;
		[RangeSC(0,1)] public SC angleMargin = 0.3f;

		[Space]
		[RangeSC(0,1)] public SC shiftMax = 0.4f;
		[RangeSC(0,1)] public SC shiftMargin = 0.3f;

		[Space]
		public Gravity g = new Gravity(0);				// 重力加速度
		public float3 windSpeed = default;				// 風速
		[HalfLifeDrag] public HalfLife airDrag = 0.5f;	// 空気抵抗
		[RangeSC(0,1)] public SC springPow = 0.1f;		// バネ係数のスケール値
		[RangeSC(0,1)] public SC maxV = 0.5f;			// 最高速度のスケール値
		[RangeSC(0,1)] public SC restorePow = 0.1f;		// 初期位置への強制戻し力
		[RangeSC(0)] public SC radius = 0.1f;			// パーティクル半径
		[JointCount(1)] public int depth = 1;			// ボーン深度
		[Range(1,10)] public int iterationNum = 1;		// 繰り返し計算回数

		[Range(0,1)] public float rotShiftRate = 0.5f;	// 回転と移動の反映割合
		public bool withAnimation = false;				// アニメーション付きのボーンに対して使用するフラグ。毎フレームデフォルト位置を再キャッシュする。
	}

	[SerializeField] internal Bone[] _bones = new []{new Bone()};

	// 衝突検出を行う対象のコライダー
	[SerializeField] internal IzBCollider.BodiesPackAuthoring _collider = null;


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
	Core.IzBPhysSpringSystem GetSys() {
		var w = World.DefaultGameObjectInjectionWorld;
		if (w == null) return null;
		return w.GetOrCreateSystem<Core.IzBPhysSpringSystem>();
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

			if (i.targets != null)
			foreach (var t in i.targets) {
				int s = 0;
				if (t.topOfBone == null) break;
				for (var j=t.topOfBone; j.childCount!=0; j=j.GetChild(0)) ++s;
				depthMax = max(s, depthMax);
			}

			i.depth = clamp(i.depth, 1, depthMax);
		}

		// 実行時はインスペクタ変更時にパラメータを同期
		if (Application.isPlaying) {
			var sys = GetSys();
			if (sys != null) sys.resetParameters(_erRegLink);
		}
	}
#endif
}
}
