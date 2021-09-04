using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

using System.Collections.Generic;


namespace IzBone.PhysCloth.Controller {
using Common;
using Common.Field;

using RangeSC = Common.Field.SimpleCurveRangeAttribute;
using SC = Common.Field.SimpleCurve;

/** IzBoneを使用するオブジェクトにつけるコンポーネントの基底クラス */
public unsafe abstract class Base : MonoBehaviour {
	// ------------------------------- inspectorに公開しているフィールド ------------------------

	// 衝突検出を行う対象のコライダー一覧
	[SerializeField] internal Common.Collider.IzCollider[] _izColliders = null;

	[Space]
	[SerializeField][RangeSC(0)] SC _r = 1;						// パーティクルの半径
	[SerializeField][RangeSC(0)] SC _m = 1;						// パーティクルの重さ
	[SerializeField][RangeSC(0,180)] SC _maxAngle = 60;			// 最大曲げ角度
	[SerializeField][RangeSC(0,1)] SC _aglRestorePow = 0;		// 曲げ角度の復元力
	[SerializeField][RangeSC(0,1)] SC _restorePow = 0;			// 初期位置への強制戻し力
	[SerializeField]SC _maxMovableRange = -1;					// 移動可能距離
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

	protected Common.Collider.Colliders _coreColliders;
	internal ConstraintMng[] _constraints;
	internal ParticleMng[] _particles;
	internal Core.World _world;

	// ジョイントの最大深度と固定深度。
	// getM系の物理パラメータ取得時に使用される。
	// パラメータ取得のみに使用されるので。厳密である必要はない。
	abstract internal int JointDepth {get;}
	virtual internal int JointDepthFixCnt => 1;


	virtual protected void Start() {
		_coreColliders = new Common.Collider.Colliders(_izColliders);
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


	// ジョイント位置の各種物理パラメータを得る処理
	internal float getR(int idx) => _r.evaluate( idx2rate(idx) );
	internal float getM(int idx) => idx<JointDepthFixCnt ? 0 : _m.evaluate( idx2rate(idx) );
	internal float getMaxAgl(int idx) => _maxAngle.evaluate( idx2rate(idx) );
	internal float getAglCompliance(int idx) =>
		ComplianceAttribute.showValue2Compliance( _aglRestorePow.evaluate( idx2rate(idx) ) * 0.2f );
	internal float getRestoreHL(int idx) =>
		HalfLifeDragAttribute.showValue2HalfLife( _restorePow.evaluate( idx2rate(idx) ) );
	internal float getMaxMovableRange(int idx) => _maxMovableRange.evaluate( idx2rate(idx) );

	float idx2rate(int idx) =>
		max(0, (idx - JointDepthFixCnt) / (JointDepth - JointDepthFixCnt - 1f) );


	// --------------------------------------------------------------------------------------------
#if UNITY_EDITOR
	/** 配下のIzColliderを全登録する */
	[ContextMenu("Collect child IzColliders")]
	void __collectChildIzCol() {
		_izColliders = GetComponentsInChildren< Common.Collider.IzCollider >();
	}

	// 実行中にプロパティが変更された場合は、次回Update時に同期を行う
	bool __need2syncManage = false;
	virtual protected void OnValidate() {
		if (Application.isPlaying) __need2syncManage = true;
	}
#endif
}

}

