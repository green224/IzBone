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

	[Space]
	[SerializeField][HalfLifeDrag] HalfLife _airDrag = 0.1f;	// 空気抵抗による半減期
	[SerializeField][Min(0)] float _maxSpeed = 100;				// 最大速度


	// --------------------------------------- publicメンバ -------------------------------------

	[Space]
	public bool useSimulation = true;				// 物理演算を行うか否か
	[Range(1,50)] public int iterationNum = 15;		// 1frame当たりの計算イテレーション回数

	[Space]
	public Gravity g = new Gravity(1);				// 重力加速度
	public float3 windSpeed = default;				// 風速

	[Space]
	// アニメーション付きのボーンに対して使用するフラグ。毎フレームデフォルト位置を再キャッシュする。
	// リグを付けたスカートなどの場合もこれをONにして使用する。
	public bool withAnimation = false;


	// ----------------------------------- private/protected メンバ -------------------------------

	protected IzBCollider.Colliders _coreColliders;
	internal ConstraintMng[] _constraints;
	internal ParticleMng[] _particles;
	internal Core.World _world;
	internal Entity _rootEntity;


	virtual protected void Start() {
		_coreColliders = new IzBCollider.Colliders(
			_collider==null ? null : _collider.Bodies
		);
		buildBuffers();
		rebuildParameters();
		_world = new Core.World( _particles, _constraints );
	}

	virtual protected void LateUpdate() {
		if (withAnimation)
			foreach (var i in _particles) i.resetDefaultPose();

		coreUpdate(Time.smoothDeltaTime);
		_world.applyToBone(_particles);
	}
//	virtual protected void FixedUpdate() {
//		coreUpdate(Time.fixedDeltaTime);
//	}

	virtual protected void OnDestroy() {
		_coreColliders?.Dispose();
		_coreColliders = null;
		_world?.Dispose();
		_world = null;
	}

	/** コアの更新処理 */
	virtual protected void coreUpdate(float dt) {

		// とりあえずdt=0のときはやらないでおく。TODO: あとで何とかする
		if (dt < 0.000001f) return;

		_coreColliders.update();

	#if UNITY_EDITOR
		// インスペクタが更新された場合は同期を行う
		if (__need2syncManage) {
			rebuildParameters();
			_world.syncWithManage(_particles, _constraints);
			__need2syncManage = false;
		}
	#endif

		_world.g = g.evaluate();
		_world.windSpeed = windSpeed;
		_world.airHL = _airDrag;		// これは計算負荷削減のためにカーブではなくスカラーで持つ
		_world.maxSpeed = _maxSpeed;
		_world.update(
			dt,
			useSimulation ? iterationNum : 0,
			_particles,
			_coreColliders
		);
	}

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

	// 実行中にプロパティが変更された場合は、次回Update時に同期を行う
	bool __need2syncManage = false;
	virtual protected void OnValidate() {
		if (Application.isPlaying) __need2syncManage = true;
	}
#endif
}

}

