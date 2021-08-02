using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

using System.Collections.Generic;


namespace IzPhysBone.Cloth.Controller {

	/** IzBoneを使用するオブジェクトにつけるコンポーネントの基底クラス */
	public unsafe abstract class Base : MonoBehaviour {
		// ------------------------------- inspectorに公開しているフィールド ------------------------

		[SerializeField] protected Collider.IzCollider[] _izColliders = null;


		// --------------------------------------- publicメンバ -------------------------------------

		[Space]
		public float3 g = float3(0,-1,0);				//!< 重力加速度
		[Range(0.01f,5)] public float airHL = 0.1f;		//!< 空気抵抗による半減期
		[Range(1,50)] public int iterationNum = 15;		//!< 1frame当たりの計算イテレーション回数


		// ----------------------------------- private/protected メンバ -------------------------------

		protected Collider.Colliders _coreColliders;
		protected List<Constraint> _constraints = new List<Constraint>();
		protected Point[] _points;
		protected Core.World _world;

		virtual protected void Start() {
			if (!Application.isPlaying) return;
			_coreColliders = new Collider.Colliders(_izColliders);
		}

		virtual protected void OnDestroy() {
			if (!Application.isPlaying) return;
			_coreColliders?.Dispose();
			_coreColliders = null;
			_world?.Dispose();
			_world = null;
		}

		virtual protected void begin() {
			// シミュレーション系を作成
			_world = new Core.World( _points, _constraints );
		}

		virtual protected void coreUpdate(float dt) {
			if (dt < 0.000001f) return;

			_coreColliders.update();

			_world.g = g;
			_world.airHL = airHL;
			for (int i=0; i<iterationNum; ++i)
				_world.update(dt/iterationNum, iterationNum, _points, _coreColliders);
		}

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
			_izColliders = GetComponentsInChildren< Collider.IzCollider >();
		}

		virtual protected void OnDrawGizmos() {
			if ( !UnityEditor.Selection.Contains( gameObject.GetInstanceID() ) ) return;

			// 登録されているコライダを表示
			if (_izColliders!=null) foreach (var i in _izColliders) i.DEBUG_drawGizmos();

			// コンストレイントを描画
			Gizmos.color = new Color(1,1,0);
			if (_constraints != null) foreach (var i in _constraints) {
				var p0 = _world.DEBUG_getPos( i.srcPointIdx );
				var p1 = _world.DEBUG_getPos( i.dstPointIdx );
				Gizmos.DrawLine( p0, p1 );
			}

			// 質点を描画
			Gizmos.color = Color.blue;
			if (_points != null) foreach (var i in _points) {
				if (i.m < 0.000001f) continue;
				var v = _world.DEBUG_getV( i.idx );
				var p = _world.DEBUG_getPos( i.idx );
//				var p = i.trans.position;
				Gizmos.DrawLine( p, p+v*0.03f );
//				Gizmos.DrawLine( p, v );
			}
		}
	#endif

		// --------------------------------------------------------------------------------------------
	}

}

