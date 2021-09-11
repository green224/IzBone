//#1define USE_ECS

using System;
using UnityEngine;

using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysCloth.Authoring {
using Common;
using Common.Field;

/** IzBoneを使用するオブジェクトにつけるコンポーネントの基底クラス */
public unsafe abstract class BaseAuthoring : MonoBehaviour {
	// ------------------------------- inspectorに公開しているフィールド ------------------------

	// 衝突検出を行う対象のコライダー一覧
	[SerializeField] internal IzBCollider.BodiesPackAuthoring _collider = null;


	// --------------------------------------- publicメンバ -------------------------------------

	[Space]
	public bool useSimulation = true;				// 物理演算を行うか否か
	[Range(1,50)] public int iterationNum = 15;		// 1frame当たりの計算イテレーション回数

	[Space]
	public Gravity g = new Gravity(1);				// 重力加速度
	public float3 windSpeed = default;				// 風速
	[UnityEngine.Serialization.FormerlySerializedAs("_airDrag")]
	[HalfLifeDrag] public HalfLife airDrag = 0.1f;	// 空気抵抗による半減期
	[UnityEngine.Serialization.FormerlySerializedAs("_maxSpeed")]
	[Min(0)] public float maxSpeed = 100;			// 最大速度

	[Space]
	// アニメーション付きのボーンに対して使用するフラグ。毎フレームデフォルト位置を再キャッシュする。
	// リグを付けたスカートなどの場合もこれをONにして使用する。
	public bool withAnimation = false;


	/** 物理状態をリセットする */
	[ContextMenu("reset")]
	public void reset() {
#if USE_ECS
		GetSys().reset(_erRegLink);
#else
#endif
	}


	// ----------------------------------- private/protected メンバ -------------------------------

#if USE_ECS
	/** ECSで得た結果をマネージドTransformに反映するためのバッファのリンク情報。System側から設定・参照される */
	Core.EntityRegisterer.RegLink _erRegLink = new Core.EntityRegisterer.RegLink();
#else
	internal Core.World _world;
	protected IzBCollider.Colliders _coreColliders;
#endif
	internal ConstraintMng[] _constraints;
	internal ParticleMng[] _particles;
	internal Entity _rootEntity = Entity.Null;


	/** メインのシステムを取得する */
	Core.IzBPhysClothSystem GetSys() {
		var w = World.DefaultGameObjectInjectionWorld;
		if (w == null) return null;
		return w.GetOrCreateSystem<Core.IzBPhysClothSystem>();
	}

	virtual protected void OnEnable() {
		buildBuffers();
		rebuildParameters();

#if USE_ECS
		var sys = GetSys();
		if (sys != null) sys.register(this, _erRegLink);
#else
		_coreColliders = new IzBCollider.Colliders(
			_collider==null ? null : _collider.Bodies
		);
		_world = new Core.World( _particles, _constraints );
#endif
	}

	virtual protected void OnDisable() {
#if USE_ECS
		var sys = GetSys();
		if (sys != null) sys.unregister(this, _erRegLink);
#else
		_coreColliders?.Dispose();
		_coreColliders = null;
		_world?.Dispose();
		_world = null;
#endif
	}

#if USE_ECS
#else
//	virtual protected void FixedUpdate() {
	virtual protected void LateUpdate() {
		if (withAnimation)
			foreach (var i in _particles) i.resetDefaultPose();

	#if UNITY_EDITOR
		// インスペクタが更新された場合は同期を行う
		if (__need2syncManage) {
			rebuildParameters();
			_world.syncWithManage(_particles, _constraints);
			__need2syncManage = false;
		}
	#endif
		var dt = Time.smoothDeltaTime;
		_world.g = g.evaluate();
		_world.windSpeed = windSpeed;
		_world.airHL = airDrag;		// これは計算負荷削減のためにカーブではなくスカラーで持つ
		_world.maxSpeed = maxSpeed;
		if (0.000001f < dt) {		// とりあえずdt=0のときはやらないでおく。TODO: あとで何とかする
			_coreColliders.update();
			_world.update(
				dt,
				useSimulation ? iterationNum : 0,
				_particles,
				_coreColliders
			);
			_world.applyToBone(_particles);
		}
	}
#endif


	/** ParticlesとConstraintsのバッファをビルドする処理。派生先で実装すること */
	abstract protected void buildBuffers();

	/** ParticlesとConstraintsのパラメータを再構築する処理。派生先で実装すること */
	abstract protected void rebuildParameters();


	// --------------------------------------------------------------------------------------------
#if UNITY_EDITOR
//	/** 配下のIzColliderを全登録する */
//	[ContextMenu("Collect child IzColliders")]
//	void __collectChildIzCol() {
//		_izColliders = GetComponentsInChildren< IzBCollider.BodyAuthoring >();
//	}

#	if USE_ECS
	virtual protected void OnValidate() {
		if (Application.isPlaying) {
			var sys = GetSys();
			if (sys != null) sys.resetParameters(_erRegLink);
		}
	}
#	else
	// 実行中にプロパティが変更された場合は、次回Update時に同期を行う
	bool __need2syncManage = false;
	virtual protected void OnValidate() {
		if (Application.isPlaying) __need2syncManage = true;
	}
#	endif
#endif
}

}

