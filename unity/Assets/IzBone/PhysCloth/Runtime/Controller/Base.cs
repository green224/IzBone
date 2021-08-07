using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

using System.Collections.Generic;


namespace IzBone.PhysCloth.Controller {
using Common;
using Common.Field;


/** IzBoneを使用するオブジェクトにつけるコンポーネントの基底クラス */
public unsafe abstract class Base : MonoBehaviour {
	// ------------------------------- inspectorに公開しているフィールド ------------------------

	[SerializeField] internal Common.Collider.IzCollider[] _izColliders = null;


	// --------------------------------------- publicメンバ -------------------------------------

	[Space]
	public bool useSimulation = true;				// 物理演算を行うか否か
	[Range(1,50)] public int iterationNum = 15;		// 1frame当たりの計算イテレーション回数

	[Space]
	public float3 g = float3(0,-1,0);				// 重力加速度
	public float3 windSpeed = default;				// 風速
	[HalfLifeDrag] public HalfLife airDrag = 0.1f;	// 空気抵抗による半減期
	[Min(0)] public float maxSpeed = 100;			// 最大速度


	// ----------------------------------- private/protected メンバ -------------------------------

	protected Common.Collider.Colliders _coreColliders;
	internal List<Constraint> _constraints = new List<Constraint>();
	internal Point[] _points;
	internal Core.World _world;

	virtual protected void Start() {
		_coreColliders = new Common.Collider.Colliders(_izColliders);
	}

	virtual protected void OnDestroy() {
		_coreColliders?.Dispose();
		_coreColliders = null;
		_world?.Dispose();
		_world = null;
	}

	/** 初期化処理。派生先から最初に一度だけ呼ぶ事 */
	virtual protected void begin() {
		// シミュレーション系を作成
		_world = new Core.World( _points, _constraints );
	}

	/** コアの更新処理 */
	virtual protected void coreUpdate(float dt) {

		// とりあえずdt=0のときはやらないでおく。TODO: あとで何とかする
		if (dt < 0.000001f) return;

		_coreColliders.update();

		_world.g = g;
		_world.windSpeed = windSpeed;
		_world.airHL = airDrag;
		_world.maxSpeed = maxSpeed;
		_world.update(
			dt,
			useSimulation ? iterationNum : 0,
			_points,
			_coreColliders
		);
	}

	/** 制約条件を追加する */
	protected void addCstr(float compliance, Point p0, Point p1) {
		if (1 <= compliance) return;
		_constraints.Add( new Constraint() {
			mode = Constraint.Mode.Distance,
			srcPointIdx = p0.idx,
			dstPointIdx = p1.idx,
			compliance = compliance,
		} );
	}

#if UNITY_EDITOR
	/** 配下のIzColliderを全登録する */
	[ContextMenu("Collect child IzColliders")]
	void collectChildIzCol() {
		_izColliders = GetComponentsInChildren< Common.Collider.IzCollider >();
	}
#endif

	// --------------------------------------------------------------------------------------------
}

}

