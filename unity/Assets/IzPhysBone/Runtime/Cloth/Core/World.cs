using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using System.Collections.Generic;
using System.Linq;

namespace IzPhysBone.Cloth.Core {
	
	/** シミュレーション系本体 */
	public unsafe sealed class World : IDisposable
	{
		// --------------------------------------- publicメンバ -------------------------------------

		public float3 g = float3(0,-1,0);		//!< 重力加速度
		public float airHL = 0.1f;					//!< 空気抵抗による半減期

		public World(
			Controller.Point[] mngPoints,
			List<Controller.Constraint> mngConstraints
		) {
			var points = mngPoints.Select(i=>new Point(i.trans.position, i.m, i.r)).ToArray();
			_points = new NativeArray<Point>(points, Allocator.Persistent);

			_constraints = new Constraints(mngConstraints, _points);
			var cnstTtlLen = _constraints.distance.Length + _constraints.axis.Length;

			_lambdas = new NativeArray<float>(cnstTtlLen, Allocator.Persistent);
		}

		/** 破棄する。必ず最後に呼ぶこと */
		public void Dispose() {
			_points.Dispose();
			_lambdas.Dispose();
			_constraints.Dispose();
		}

		/** 更新する */
		public void update(
			float dt,
			int iterationNum,
			Controller.Point[] mngPoints,
			Collider.Colliders colliders
		) {
			var airResRate = Mathf.Pow( 2, -dt / airHL );

			var pntsPtr0 = (Point*)_points.GetUnsafePtr();
			var pntsPtrEnd = pntsPtr0 + _points.Length;
			var lmdsPtr0 = (float*)_lambdas.GetUnsafePtr();
			var lmdsPtrEnd = lmdsPtr0 + _lambdas.Length;

			// 質点の更新
			{
				var sqDt = dt*dt;
				int i = 0;
				for (var p=pntsPtr0; p!=pntsPtrEnd; ++p, ++i) {
					if ( p->invM < MinimumM ) {
						p->col.pos = mngPoints[i].trans.position;
						p->v = default;
					} else {
//						var a = p->pos;
//						p->pos += (a - p->oldPos)*airResRate + sqDt*g;
//						p->oldPos = a;
//						var v = p->pos - p->v;
						var v = p->v * dt;
						var vNrom = length(v);
						v *= ( Mathf.Min(vNrom,0.006f) / (vNrom+0.0000001f) );
						
						p->v = p->col.pos;
						p->col.pos += v*airResRate + sqDt*g;
					}
				}
			}

			{// 物理計算
				static void solveCollider<T>(Point* p, NativeArray<T> colliders)
				where T : struct, Collider.ICollider {
					if (!colliders.IsCreated) return;
					for (int i=0; i<colliders.Length; ++i) colliders[i].solve(&p->col);
				}

				static void solveConstraints<T>(float sqDt, float* lambda, NativeArray<T> constraints)
				where T : struct, IConstraint {
					if (!constraints.IsCreated) return;
					for (int i=0; i<constraints.Length; ++i, ++lambda)
						*lambda += constraints[i].solve( sqDt, *lambda );
				}

				for (var p=lmdsPtr0; p!=lmdsPtrEnd; ++p) *p=0;

				var sqDt = dt*dt;
				for (int i=0; i<iterationNum; ++i) {

					for (var p=pntsPtr0; p!=pntsPtrEnd; ++p) {
						if (p->invM == 0) continue;
						solveCollider(p, colliders.spheres);
						solveCollider(p, colliders.capsules);
						solveCollider(p, colliders.boxes);
						solveCollider(p, colliders.planes);
					}

					var lambda = lmdsPtr0;
					solveConstraints(sqDt, lambda, _constraints.distance);
					solveConstraints(sqDt, lambda, _constraints.axis);
				}
			}

			// 速度の保存
			for (var p=pntsPtr0; p!=pntsPtrEnd; ++p)
				p->v = (p->col.pos - p->v) / dt;

			// 質点の反映
			{
				for (int i=0; i<_points.Length; ++i) {

					var j=mngPoints[i];
					if ( j.parent != null ) continue;

					if (MinimumM <= j.m) {
						j.trans.position = _points[i].col.pos;
					}

					for (j=j.child; j!=null; j=j.child) {
						var point = pntsPtr0 + j.idx;

						j.trans.parent.localRotation = Unity.Mathematics.quaternion.identity;
						var q = Math8.FromToRotation(
							j.trans.localPosition,
							mul(
								j.trans.parent.worldToLocalMatrix,
								float4( point->col.pos, 1 )
							).xyz
						);
						j.trans.parent.localRotation = q;
//						j.trans.position = point.pos;
						point->col.pos = j.trans.position;

//						var p = j.trans.position - point.pos;
//						point.pos += p;
//						point.oldPos += p;
					}
				}
			}
		}

	#if UNITY_EDITOR
		public float3 DEBUG_getPos(int idx) {
			if (!_points.IsCreated || _points.Length<=idx) return default;
			return _points[idx].col.pos;
		}
		public float3 DEBUG_getV(int idx) {
			if (!_points.IsCreated || _points.Length<=idx) return default;
			return _points[idx].v;
		}
	#endif


		// ----------------------------------- private/protected メンバ -------------------------------

		const float MinimumM = 0.00000001f;
		NativeArray<Point> _points;
		Constraints _constraints;
		NativeArray<float> _lambdas;

		~World() {
			if ( _points.IsCreated ) {
				Debug.LogError("World is not disposed");
				Dispose();
			}
		}


		// --------------------------------------------------------------------------------------------
	}

}
