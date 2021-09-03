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

	[Serializable] internal sealed class PhysParam {
		[RangeSC(0)] public SC m = 1;
		[RangeSC(0)] public SC r = 1;
		[RangeSC(0,180)] public SC maxAngle = 60;
		[RangeSC(0,1)] public SC aglRestorePow = 0;
		[RangeSC(0,1)] public SC restorePow = 0;
		public SC maxMovableRange = -1;

		public float getM(float bRate) => bRate<0 ? 0 : m.evaluate( bRate );
		public float getR(float bRate) => r.evaluate( max(0,bRate) );
		public float getMaxAgl(float bRate) => maxAngle.evaluate( max(0,bRate) );
		public float getAglCompliance(float bRate) =>
			ComplianceAttribute.showValue2Compliance(
				aglRestorePow.evaluate( max(0,bRate) ) * 0.2f );
		public float getRestoreHL(float bRate) =>
			HalfLifeDragAttribute.showValue2HalfLife(
				restorePow.evaluate( max(0,bRate) ) );
		public float getMaxMovableRange(float bRate) =>
			maxMovableRange.evaluate( max(0,bRate) );

		static public float idx2rate(int idx, int depth, int fixCount) =>
			idx<fixCount ? -1 : ( (idx-fixCount) / (depth-fixCount-1f) );
	}

	[SerializeField] internal Common.Collider.IzCollider[] _izColliders = null;


	// --------------------------------------- publicメンバ -------------------------------------

	[Space]
	public bool useSimulation = true;				// 物理演算を行うか否か
	[Range(1,50)] public int iterationNum = 15;		// 1frame当たりの計算イテレーション回数

	[Space]
	public Gravity g = new Gravity(1);					// 重力加速度
	public float3 windSpeed = default;					// 風速
	[HalfLifeDrag] public HalfLife airDrag = 0.1f;		// 空気抵抗による半減期
	[Min(0)] public float maxSpeed = 100;				// 最大速度

	[Space]
	// アニメーション付きのボーンに対して使用するフラグ。毎フレームデフォルト位置を再キャッシュする。
	// リグを付けたスカートなどの場合もこれをONにして使用する。
	public bool withAnimation = false;


	// ----------------------------------- private/protected メンバ -------------------------------

	protected Common.Collider.Colliders _coreColliders;
	internal ConstraintMng[] _constraints;
	internal ParticleMng[] _particles;
	internal Core.World _world;

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
		_world.airHL = airDrag;
		_world.maxSpeed = maxSpeed;
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

