using System;
using UnityEngine;

using System.Runtime.InteropServices;
using System.Linq;

namespace IzBone.Core {
	
	/** シミュレーション系本体 */
	public unsafe sealed class World {
		// --------------------------------------- publicメンバ -------------------------------------

		public Vector3 g = new Vector3(0,-1,0);		//!< 重力加速度
		public float airHL = 0.1f;					//!< 空気抵抗による半減期

		/** 初期化する。最初に一度だけ呼ぶこと */
		public void setup(
			Controller.Point[] mngPoints,
			Controller.Constraint[] mngConstraints
		) {
			var points = mngPoints.Select(i=>new Point(i.trans.position, i.m, i.r)).ToArray();
			_points.reset(points);

			_constraints.reset(mngConstraints, _points);
			var cnstTtlLen = _constraints.distance.length + _constraints.axis.length;

			if (_lambdas.array==null || _lambdas.length < cnstTtlLen )
				_lambdas.reset( new float[cnstTtlLen] );
		}

		/** 破棄する。必ず最後に呼ぶこと */
		public void release() {
			_points.reset( null );
			_lambdas.reset( null );
			_constraints.release();
		}

		/** 更新する */
		public void update(
			float dt,
			int iterationNum,
			Controller.Point[] mngPoints,
			Colliders colliders
		) {
			var airResRate = Mathf.Pow( 2, -dt / airHL );

			// 質点の更新
			{
				var sqDt = dt*dt;
				int i = 0;
				for (var p=_points.ptr; p!=_points.ptrEnd; ++p, ++i) {
					if ( p->invM < MinimumM ) {
						p->pos = mngPoints[i].trans.position;
						p->v = new Vector3(0,0,0);
					} else {
//						var a = p->pos;
//						p->pos += (a - p->oldPos)*airResRate + sqDt*g;
//						p->oldPos = a;
//						var v = p->pos - p->v;
						var v = p->v * dt;
						var vNrom = v.magnitude;
						v *= ( Mathf.Min(vNrom,0.006f) / (vNrom+0.0000001f) );
						
						p->v = p->pos;
						p->pos += v*airResRate + sqDt*g;
					}
				}
			}

			{// 物理計算
				for (var p=_lambdas.ptr; p!=_lambdas.ptrEnd; ++p) *p=0;

				var sqDt = dt*dt;
				for (int i=0; i<iterationNum; ++i) {

					for (var p=_points.ptr; p!=_points.ptrEnd; ++p) {
						if (p->invM == 0) continue;
						for (var c=colliders.spheres.ptr;  c!=colliders.ptrEnd_s; ++c) c->solve(p);
						for (var c=colliders.capsules.ptr; c!=colliders.ptrEnd_c; ++c) c->solve(p);
						for (var c=colliders.boxes.ptr;    c!=colliders.ptrEnd_b; ++c) c->solve(p);
						for (var c=colliders.planes.ptr;   c!=colliders.ptrEnd_p; ++c) c->solve(p);
					}

					var lamdbda = _lambdas.ptr;
					for (
						var p=_constraints.distance.ptr;
						p!=_constraints.distance.ptrEnd;
						++p, ++lamdbda
					) *lamdbda += p->solve( sqDt, *lamdbda );
					for (
						var p=_constraints.axis.ptr;
						p!=_constraints.axis.ptrEnd;
						++p, ++lamdbda
					) *lamdbda += p->solve( sqDt, *lamdbda );
				}
			}

			// 速度の保存
			for (var p=_points.ptr; p!=_points.ptrEnd; ++p)
				p->v = (p->pos - p->v) / dt;

			// 質点の反映
			{
				for (int i=0; i<_points.length; ++i) {

					var j=mngPoints[i];
					if ( j.parent != null ) continue;

					if (MinimumM <= j.m) {
						j.trans.position = _points.array[i].pos;
					}

					for (j=j.child; j!=null; j=j.child) {
						ref Point point = ref _points.array[j.idx];

						j.trans.parent.localRotation = Quaternion.identity;
						var q = Quaternion.FromToRotation(
							j.trans.localPosition.normalized,
							j.trans.parent.worldToLocalMatrix.MultiplyPoint( point.pos ).normalized
						);
						j.trans.parent.localRotation = q;
//						j.trans.position = point.pos;
						point.pos = j.trans.position;

//						var p = j.trans.position - point.pos;
//						point.pos += p;
//						point.oldPos += p;
					}
				}
			}
		}

	#if UNITY_EDITOR
		public Vector3 DEBUG_getPos(int idx) {
			if ( _points.length <= idx ) return Vector3.zero;
			return _points.array[idx].pos;
		}
		public Vector3 DEBUG_getV(int idx) {
			if ( _points.length <= idx ) return Vector3.zero;
			return _points.array[idx].v;
		}
	#endif


		// ----------------------------------- private/protected メンバ -------------------------------

		const float MinimumM = 0.00000001f;
		PinnedArray<Point> _points = new PinnedArray<Point>();
		Constraints _constraints = new Constraints();
		PinnedArray<float> _lambdas = new PinnedArray<float>();


		// --------------------------------------------------------------------------------------------
	}

}
