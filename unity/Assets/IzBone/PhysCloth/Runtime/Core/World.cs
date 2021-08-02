using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using System.Collections.Generic;
using System.Linq;

namespace IzBone.PhysCloth.Core {
	using Common;

	/** シミュレーション系本体 */
	public unsafe sealed class World : IDisposable
	{
		// --------------------------------------- publicメンバ -------------------------------------

		public float3 g = float3(0,-1,0);		// 重力加速度
		public float3 windSpeed = default;		// 風速
		public float airHL = 0.1f;				// 空気抵抗による半減期

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

		/** 更新する。dtに0は入れない事 */
		public void update(
			float dt,
			int iterationNum,
			Controller.Point[] mngPoints,
			Common.Collider.Colliders colliders
		) {
			// 各種バッファのポインタを取得しておく
			var pntsPtr0 = (Point*)_points.GetUnsafePtr();
			var pntsPtrEnd = pntsPtr0 + _points.Length;
			var lmdsPtr0 = (float*)_lambdas.GetUnsafePtr();
			var lmdsPtrEnd = lmdsPtr0 + _lambdas.Length;

			// イテレーションが0回の場合は位置キャッシュだけ更新する
			if (iterationNum == 0) {
				int i = 0;
				for (var p=pntsPtr0; p!=pntsPtrEnd; ++p,++i) {
					var lastPos = p->col.pos;
					p->col.pos = mngPoints[i].trans.position;
					p->v = (p->col.pos - lastPos) / dt;
				}
				return;
			}

			// 空気抵抗の値を計算
			var airResRate = Math8.calcHL( airHL, dt );
			var airResRateIntegral = Math8.calcIntegralHL( airHL, dt );

			// 質点の位置の更新
			{
				var sqDt = dt*dt;
				int i = 0;
				for (var p=pntsPtr0; p!=pntsPtrEnd; ++p,++i) {
					if ( p->invM < MinimumM ) {
						p->col.pos = mngPoints[i].trans.position;
					} else {
						var v = p->v;
						var vNrom = length(v);
						
						// 更新前の位置をvに入れておく。これは後で参照するための一時的なキャッシュ用
						p->v = p->col.pos;

						// 位置を更新する。
						// 空気抵抗の影響を与えるため、以下のようにしている。
						// 一行目:
						//    dtは変動するので、空気抵抗の影響が解析的に正しく影響するように、
						//    vには積分結果のairResRateIntegralを掛ける。
						//    空気抵抗がない場合は、gの影響は g*dt^2になるが、
						//    ここもいい感じになるようにg*dt*airResRateIntegralとしている。
						//    これは正しくはないが、いい感じに見える。
						// 二行目:
						//    １行目だけの場合は空気抵抗によって速度が0になるように遷移する。
						//    風速の影響を与えたいため、空気抵抗による遷移先がwindSpeedになるようにしたい。
						//    airResRateIntegralは空気抵抗の初期値1の減速曲線がグラフ上に描く面積であるので,
						//    減速が一切ない場合に描く面積1*dtとの差は、dt-airResRateIntegralとなる。
						//    したがってこれにwindSpeedを掛けて、風速に向かって空気抵抗がかかるようにする。
						p->col.pos +=
							(v + g*dt) * airResRateIntegral +
							windSpeed * (dt - airResRateIntegral);
					}
				}
			}

			{// XPBDによるフィッティング処理
				static void solveCollider<T>(Point* p, NativeArray<T> colliders)
				where T : struct, Common.Collider.ICollider {
					if (!colliders.IsCreated) return;
					for (int i=0; i<colliders.Length; ++i) colliders[i].solve(&p->col);
				}

				static void solveConstraints<T>(float sqDt, float* lambda, NativeArray<T> constraints)
				where T : struct, IConstraint {
					if (!constraints.IsCreated) return;
					for (int i=0; i<constraints.Length; ++i,++lambda)
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
//var x = j.defaultParentRot * j.trans.localPosition.normalized;
//var a = j.trans.parent.worldToLocalMatrix.MultiplyPoint( point.pos ).normalized;
//var z = Vector3.Cross(x,a).normalized;
//var y = Vector3.Cross(z,x).normalized;
//var agl = Mathf.Atan2(Vector3.Dot(y,a), Vector3.Dot(x,a));
//var q = Quaternion.AngleAxis( agl*Mathf.Rad2Deg, z ) * j.defaultParentRot;

						// 回転する元方向と先方向
						var from = mul( j.defaultParentRot, j.trans.localPosition );
						var to = mul(
							j.trans.parent.worldToLocalMatrix,
							float4( point->col.pos, 1 )
						).xyz;

						// 回転軸と角度を計算 refer:Math8.fromToRotation
						var axis = math.normalizesafe( math.cross(from, to) );
						var theta = math.acos( math.clamp(
							math.dot( math.normalizesafe(from), math.normalizesafe(to) ),
							-1, 1
						) );

						// 角度制限を反映
						theta = min( theta, radians(j.maxAngle) );

						// Quaternionを生成
						var s = math.sin(theta / 2);
						var c = math.cos(theta / 2);
						var q = math.quaternion(axis.x*s, axis.y*s, axis.z*s, c);

						// 初期姿勢を反映
						q = mul(q, j.defaultParentRot);

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
