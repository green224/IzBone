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
	using Common.Field;

	/** シミュレーション系本体 */
	public unsafe sealed class World : IDisposable
	{
		// --------------------------------------- publicメンバ -------------------------------------

		public float3 g = float3(0,-1,0);		// 重力加速度
		public float3 windSpeed = default;		// 風速
		public HalfLife airHL = 0.1f;			// 空気抵抗による半減期
		public float maxSpeed = 100;			// 最大速度

		public World(
			Controller.ParticleMng[] mngParticles,
			Controller.ConstraintMng[] mngConstraints
		) {
			// particlesを生成
			var ptcls = mngParticles
				.Select(i=>{
					var p0 = i.trans.position;
					var pp = i.parent?.trans?.position;
					var pc = i.child?.trans?.position;
					var pl = i.left?.trans?.position;
					var pr = i.right?.trans?.position;
					float3 nml = 0;
					if ( pp.HasValue && pl.HasValue )
						nml += normalize(cross( pp.Value - p0, pl.Value - p0 ));
					if ( pl.HasValue && pc.HasValue )
						nml += normalize(cross( pl.Value - p0, pc.Value - p0 ));
					if ( pc.HasValue && pr.HasValue )
						nml += normalize(cross( pc.Value - p0, pr.Value - p0 ));
					if ( pr.HasValue && pp.HasValue )
						nml += normalize(cross( pr.Value - p0, pp.Value - p0 ));

					return new Particle( p0, normalizesafe(nml) );
				}).ToArray();
			_particles = new NativeArray<Particle>(ptcls, Allocator.Persistent);

			// パラメータを同期
			syncWithManage(mngParticles, mngConstraints);
		}

		/** 各種パラメータをマネージ空間のものと同期する。これはEditorで実行中にパラメータが変更された際に呼ぶ */
		public void syncWithManage(
			Controller.ParticleMng[] mngParticles,
			Controller.ConstraintMng[] mngConstraints
		) {
			// particlesを更新
			var ptclPtr0 = (Particle*)_particles.GetUnsafePtr();
			var ptclPtrEnd = ptclPtr0 + _particles.Length;
			int i=0;
			for (var p=ptclPtr0; p!=ptclPtrEnd; ++p,++i) {
				var m = mngParticles[i];
				p->syncParams( m.m, m.r, m.restoreHL );
			}

			// constraintsを再生成
			_constraints.Dispose();
			_constraints = new Constraints(mngConstraints, _particles);
			var cnstTtlLen = _constraints.distance.Length + _constraints.axis.Length;

			// lambdasを生成
			if (_lambdas.IsCreated) _lambdas.Dispose();
			_lambdas = new NativeArray<float>(cnstTtlLen, Allocator.Persistent);
		}

		/** 破棄する。必ず最後に呼ぶこと */
		public void Dispose() {
			_particles.Dispose();
			_lambdas.Dispose();
			_constraints.Dispose();
		}

		/** 更新する。dtに0は入れない事 */
		public void update(
			float dt,
			int iterationNum,
			Controller.ParticleMng[] mngParticles,
			Common.Collider.Colliders colliders
		) {
			// 各種バッファのポインタを取得しておく
			var ptclPtr0 = (Particle*)_particles.GetUnsafePtr();
			var ptclPtrEnd = ptclPtr0 + _particles.Length;
			var lmdsPtr0 = (float*)_lambdas.GetUnsafePtr();
			var lmdsPtrEnd = lmdsPtr0 + _lambdas.Length;

			// イテレーションが0回の場合は位置キャッシュだけ更新する
			if (iterationNum == 0) {
				int i = 0;
				for (var p=ptclPtr0; p!=ptclPtrEnd; ++p,++i) {
					var lastPos = p->col.pos;
					p->col.pos = mngParticles[i].trans.position;
					p->v = (p->col.pos - lastPos) / dt;
				}
				return;
			}
			
			{// デフォルト姿勢でのL2Wを計算しておく
				int i = 0;
				for (var p=ptclPtr0; p!=ptclPtrEnd; ++p,++i) {
					if (mngParticles[i].parent != null) continue;

					var parentTrans = mngParticles[i].trans;
					var l2w = parentTrans == null
						? Unity.Mathematics.float4x4.identity
						: (float4x4)mngParticles[i].trans.parent.localToWorldMatrix;

					for (var m=mngParticles[i]; m!=null; m=m.child) {
						l2w = mul( l2w, m.defaultL2P );

						ptclPtr0[m.idx].defaultL2W = l2w;
					}
				}
			}

			// 空気抵抗の値を計算
			var airResRate = HalfLifeDragAttribute.evaluate( airHL, dt );
			var airResRateIntegral = HalfLifeDragAttribute.evaluateIntegral( airHL, dt );

			// 質点の位置の更新
			{
				int i = 0;
				for (var p=ptclPtr0; p!=ptclPtrEnd; ++p,++i) {
					if ( p->invM < MinimumM ) {
						p->col.pos = mngParticles[i].trans.position;
					} else {
						var v = p->v;
						
						// 速度制限を適応
						v = clamp(-maxSpeed, v, maxSpeed);

						// 更新前の位置をvに入れておく。これは後で参照するための一時的なキャッシュ用
						p->v = p->col.pos;

						// 位置を物理で更新する。
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

						// 初期位置に戻すようなフェードを掛ける
						p->col.pos = lerp(
							p->defaultL2W.c3.xyz,
							p->col.pos,
							HalfLifeDragAttribute.evaluate( p->restoreHL, dt )
						);
					}
				}
			}

			{// XPBDによるフィッティング処理
				static void solveCollider<T>(Particle* p, NativeArray<T> colliders)
				where T : struct, Common.Collider.ICollider {
					if (!colliders.IsCreated) return;
					for (int i=0; i<colliders.Length; ++i) colliders[i].solve(&p->col);
				}

				static void solveConstraints<T>(float sqDt, ref float* lambda, NativeArray<T> constraints)
				where T : struct, IConstraint {
					if (!constraints.IsCreated) return;
					for (int i=0; i<constraints.Length; ++i,++lambda)
						*lambda += constraints[i].solve( sqDt, *lambda );
				}

				for (var p=lmdsPtr0; p!=lmdsPtrEnd; ++p) *p=0;

				var sqDt = dt*dt/iterationNum/iterationNum;
				for (int i=0; i<iterationNum; ++i) {

					// コライダとの衝突解決
					for (var p=ptclPtr0; p!=ptclPtrEnd; ++p) {
						if (p->invM == 0) continue;
						solveCollider(p, colliders.spheres);
						solveCollider(p, colliders.capsules);
						solveCollider(p, colliders.boxes);
						solveCollider(p, colliders.planes);
					}

					// フィッティング
					var lambda = lmdsPtr0;
					solveConstraints(sqDt, ref lambda, _constraints.distance);
					solveConstraints(sqDt, ref lambda, _constraints.axis);
				}

				// 最後にもう一度コライダとの衝突解決
				for (var p=ptclPtr0; p!=ptclPtrEnd; ++p) {
					if (p->invM == 0) continue;
					solveCollider(p, colliders.spheres);
					solveCollider(p, colliders.capsules);
					solveCollider(p, colliders.boxes);
					solveCollider(p, colliders.planes);
				}
			}

			// 速度の保存
			for (var p=ptclPtr0; p!=ptclPtrEnd; ++p)
				p->v = (p->col.pos - p->v) / dt;

			// 質点の反映
			{
				for (int i=0; i<_particles.Length; ++i) {

					var j=mngParticles[i];
					if ( j.parent != null ) continue;

					// 位置変化の適応はとりあえず無しで
//					if (MinimumM <= j.m) {
//						j.trans.position = _particles[i].col.pos;
//					}

					for (j=j.child; j!=null; j=j.child) {
						var ptcl = ptclPtr0 + j.idx;

						j.trans.parent.localRotation = Unity.Mathematics.quaternion.identity;

						// 回転する元方向と先方向
						var from = mul( j.defaultParentRot, j.trans.localPosition );
						var to = mul(
							j.trans.parent.worldToLocalMatrix,
							float4( ptcl->col.pos, 1 )
						).xyz;

						// 回転軸と角度を計算 refer:Math8.fromToRotation
						var axis = normalizesafe( cross(from, to) );
						var theta = acos( clamp(
							dot( normalizesafe(from), normalizesafe(to) ),
							-1, 1
						) );

						// 角度制限を反映
						// TODO : 角度制限はここではなく、Constraintで行うようにする
						theta = min( theta, radians(j.maxAngle) );

						// Quaternionを生成
						var s = sin(theta / 2);
						var c = cos(theta / 2);
						var q = quaternion(axis.x*s, axis.y*s, axis.z*s, c);

						// 初期姿勢を反映
						q = mul(q, j.defaultParentRot);

						j.trans.parent.localRotation = q;
//						j.trans.position = ptcl.pos;
						ptcl->col.pos = j.trans.position;
					}
				}
			}
		}

	#if UNITY_EDITOR
		public float3 DEBUG_getPos(int idx) {
			if (!_particles.IsCreated || _particles.Length<=idx) return default;
			return _particles[idx].col.pos;
		}
		public float3 DEBUG_getV(int idx) {
			if (!_particles.IsCreated || _particles.Length<=idx) return default;
			return _particles[idx].v;
		}
	#endif


		// ----------------------------------- private/protected メンバ -------------------------------

		const float MinimumM = 0.00000001f;
		NativeArray<Particle> _particles;
		Constraints _constraints;
		NativeArray<float> _lambdas;

		~World() {
			if ( _particles.IsCreated ) {
				Debug.LogError("World is not disposed");
				Dispose();
			}
		}


		// --------------------------------------------------------------------------------------------
	}

}
