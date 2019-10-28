using System;
using UnityEngine;

using System.Collections.Generic;


namespace IzBone.Controller {

	/**
	 * IzBoneを使用するオブジェクトにつけるコンポーネント。
	 * 平面的な布のようなものを再現する際に使用する
	 */
	[ExecuteInEditMode]
	[AddComponentMenu("IzBone/Rope")]
	public unsafe sealed class Rope : Base {
		// ------------------------------- inspectorに公開しているフィールド ------------------------

		/** 骨の情報 */
		[Serializable] sealed class BoneInfo {
			public Transform boneTop = null;
			public int depth = 1;
			[SerializeField] AnimationCurve _m = null;
			[SerializeField] AnimationCurve _r = null;

			[NonSerialized] public Point point = null;

			public float getM(int idx) => Math.Max(0, _m.Evaluate( depth==1 ? 0 : ((float)idx/(depth-1)) ) );
			public float getR(int idx) => Math.Max(0, _r.Evaluate( depth==1 ? 0 : ((float)idx/(depth-1)) ) );
		}
		[SerializeField] BoneInfo _boneInfo = null;

		[Space]
		[Compliance][SerializeField] float _cmpl_direct = 0.000000001f;		//!< Compliance値 直接接続
		[Compliance][SerializeField] float _cmpl_bend = 0.00002f;			//!< Compliance値 曲げ用の１つ飛ばし接続
		[Compliance][SerializeField] float _cmpl_roll = 0.0000001f;			//!< Compliance値 捻じれ用の対角線接続


		// --------------------------------------- publicメンバ -------------------------------------

		[Space]
		public Vector3 g = new Vector3(0,-1,0);			//!< 重力加速度
		[Range(0.01f,5)] public float airHL = 0.1f;		//!< 空気抵抗による半減期
		[Range(1,50)] public int iterationNum = 15;		//!< 1frame当たりの計算イテレーション回数


		// ----------------------------------- private/protected メンバ -------------------------------

		Point[] _points = null;
		Constraint[] _constraints = null;
		Core.World _world = null;

		override protected void Start() {
			base.Start();
			if (!Application.isPlaying) return;

			// 質点リストを構築
			var points = new List<Point>();
			{
				Point p = _boneInfo.point = new Point() {
					idx = points.Count,
					trans = _boneInfo.boneTop,
					m = _boneInfo.getM(0),
					r = _boneInfo.getR(0),
				};
				points.Add(p);

				int k = 1;
				for (var j=p.trans; k<_boneInfo.depth; ++k) {
					j=j.GetChild(0);
					var newP = new Point() {
						idx = points.Count,
						trans = j,
						parent = p,
						m = _boneInfo.getM(k),
						r = _boneInfo.getR(k),
					};
					p.child = newP;
					p = newP;
					points.Add(p);
				}
			}
			_points = points.ToArray();

			// 制約リストを構築
			var constraints = new List<Constraint>();
			Action<Point, Point, int> addCstr =
				(p0, p1, type) => {
					var compliance = type==0 ? _cmpl_direct : (type==1?_cmpl_bend:_cmpl_roll);
					if (compliance < 1)
						constraints.Add( new Constraint() {
							mode = Constraint.Mode.Distance,
							srcPointIdx = p0.idx,
							dstPointIdx = p1.idx,
							compliance = compliance,
						} );
				};
			{
				var c = _boneInfo.point;
				var d0 = c.child;
				var d1 = d0?.child;

				int depth=0;
				while (c!=null) {
					if (d0!=null) addCstr(c,d0,0);
					if (d1!=null) addCstr(c,d1,1);

					c=c.child;
					d0=d0?.child;
					d1=d1?.child;
					++depth;
				}
			}
			_constraints = constraints.ToArray();

			// シミュレーション系を作成
			_world = new Core.World();
			_world.setup( _points, _constraints );
		}

	#if UNITY_EDITOR
		void Update() {
			if (Application.isPlaying) return;

			if (_boneInfo == null) return;
			{
				// Depthを有効範囲に丸める
				if ( _boneInfo.boneTop == null ) {
					_boneInfo.depth = 0;
				} else {
					int depthMax = 1;
					for (var j=_boneInfo.boneTop; j.childCount!=0; j=j.GetChild(0)) ++depthMax;

					_boneInfo.depth = Mathf.Clamp(_boneInfo.depth, 1, depthMax);
				}
			}
		}
	#endif
			
		void LateUpdate() {
			if (!Application.isPlaying) return;
			coreUpdate(Time.deltaTime);
		}

		override protected void OnDestroy() {
			base.OnDestroy();
			if (!Application.isPlaying) return;
			_world.release();
		}

		override protected void coreUpdate(float dt) {
			base.coreUpdate(dt);
			_world.g = g;
			_world.airHL = airHL;
			for (int i=0; i<iterationNum; ++i)
				_world.update(dt/iterationNum, iterationNum, _points, _coreColliders);
		}


	#if UNITY_EDITOR
		override protected void OnDrawGizmos() {
			base.OnDrawGizmos();
			if ( !UnityEditor.Selection.Contains( gameObject.GetInstanceID() ) ) return;

			if (_boneInfo == null) return;
			if (_boneInfo.boneTop != null) {

				var trans = _boneInfo.boneTop;
				for (int dCnt=0; dCnt!=_boneInfo.depth; ++dCnt) {
					Gizmos.color = new Color(1,1,1,0.5f);
					Gizmos.DrawSphere( trans.position, _boneInfo.getR(dCnt) );

					if (!Application.isPlaying) {
						Gizmos.color = new Color(1,1,0);
						if (dCnt != 0) Gizmos.DrawLine( trans.position, trans.parent.position );
					}

					if ( trans.childCount==0 ) break; else trans=trans.GetChild(0);
				}
			}

			Gizmos.color = new Color(1,1,0);
			if (_constraints != null) foreach (var i in _constraints) {
				var p0 = _world.DEBUG_getPos( i.srcPointIdx );
				var p1 = _world.DEBUG_getPos( i.dstPointIdx );
				Gizmos.DrawLine( p0, p1 );
			}

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

