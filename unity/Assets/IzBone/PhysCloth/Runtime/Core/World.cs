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
		// TODO : これをカーブで設定できるようにする
		public HalfLife airHL = 0.1f;			// 空気抵抗による半減期
		public float maxSpeed = 100;			// 最大速度

		public World(
			Controller.ParticleMng[] mngParticles,
			Controller.ConstraintMng[] mngConstraints
		) {
			// particlesを生成
			_particles = new NativeArray<Particle>(mngParticles.Length, Allocator.Persistent);
			for (int i=0; i<mngParticles.Length; ++i) {
				var mp = mngParticles[i];

				var p0 = mp.trans.position;
				var pp = mp.parent?.trans?.position;
				var pc = mp.child?.trans?.position;
				var pl = mp.left?.trans?.position;
				var pr = mp.right?.trans?.position;
				float3 nml = 0;
				if ( pp.HasValue && pl.HasValue )
					nml += normalize(cross( pp.Value - p0, pl.Value - p0 ));
				if ( pl.HasValue && pc.HasValue )
					nml += normalize(cross( pl.Value - p0, pc.Value - p0 ));
				if ( pc.HasValue && pr.HasValue )
					nml += normalize(cross( pc.Value - p0, pr.Value - p0 ));
				if ( pr.HasValue && pp.HasValue )
					nml += normalize(cross( pr.Value - p0, pp.Value - p0 ));

				// 法線が得られなかった場合は、とりあえず親の法線をコピーする
				if (nml.Equals(0)) {
					if (mp.parent != null) nml = _particles[i-1].initWNml;
				} else {
					nml = normalize(nml);
				}

				// 法線の変換は逆転置行列
				// 参考:https://raytracing.hatenablog.com/entry/20130325/1364229762
				var lNml = mul(
					transpose( (float3x3)(float4x4)mp.trans.localToWorldMatrix ),
					nml
				);

				_particles[i] = new Particle(i, p0, nml, lNml, mp.angleCompliance);
			}

			// particleChainsを生成
			var ptclPtr0 = (Particle*)_particles.GetUnsafePtr();
			var chains = mngParticles
				.Where(i => i.parent==null)
				.Select(i => {
					int length = 0;
					for (var j=i; j!=null; j=j.child) ++length;

					return new ParticleChain( ptclPtr0+i.idx, length );
				}).ToArray();
			_particleChains = new NativeArray<ParticleChain>(chains, Allocator.Persistent);

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
				p->syncParams( m.m, m.r, radians(m.maxAngle), m.angleCompliance, m.restoreHL );
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
			_particleChains.Dispose();
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
			var chainPtr0 = (ParticleChain*)_particleChains.GetUnsafePtr();
			var chainPtrEnd = chainPtr0 + _particleChains.Length;
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
			
			{// デフォルト姿勢でのL2Wと法線を計算しておく
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
						ptclPtr0[m.idx].defaultWNml =
							mul( (float3x3)l2w, ptclPtr0[m.idx].initLNml );
					}
				}
			}

			// 空気抵抗の値を計算
			var airResRate = HalfLifeDragAttribute.evaluate( airHL, dt );
			var airResRateIntegral = HalfLifeDragAttribute.evaluateIntegral( airHL, dt );

			// 質点の位置を更新
			for (var c=chainPtr0; c!=chainPtrEnd; ++c) {
				for (int i=0; i!=c->length; ++i) {
					var p = c->begin + i;

					if (p->invM < MinimumM) {
						var mngP = mngParticles[p->index];
//						if (i==0) {
							p->col.pos = mngP.trans.position;
//						} else {
//							var l2w =
//								mngP.trans.parent.localToWorldMatrix *
//								Matrix4x4.TRS(
//									mngP.trans.localPosition,
//									mngP.trans.localRotation,
//									mngP.trans.localScale
//								);
//							p->col.pos = ( (float4x4)l2w ).c3.xyz;
//						}
					} else {
						var v = p->v;
						
						// 速度制限を適応
						v = clamp(v, -maxSpeed, maxSpeed);

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

				// λを初期化
				for (var p=lmdsPtr0; p!=lmdsPtrEnd; ++p) *p=0;
				var cldCstLmd = new NativeArray<float>(_particles.Length, Allocator.Temp);
				var cldCstLmdPtr0 = (float*)cldCstLmd.GetUnsafePtr();
				var aglLmtLmd = new NativeArray<float>(_particles.Length, Allocator.Temp);
				var aglLmtLmdPtr0 = (float*)aglLmtLmd.GetUnsafePtr();

				// フィッティングループ
				var sqDt = dt*dt/iterationNum/iterationNum;
				for (int i=0; i<iterationNum; ++i) {

					// まず現在の位置での角度制限の拘束条件を適応
					for (var c=chainPtr0; c!=chainPtrEnd; ++c) {
						if (c->length == 1) {
							c->begin->dWRot = Unity.Mathematics.quaternion.identity;
						} else {
							var q0 = Unity.Mathematics.quaternion.identity;
							var q1 = Unity.Mathematics.quaternion.identity;
							var q2 = Unity.Mathematics.quaternion.identity;
							Particle* p0 = null;
							Particle* p1 = null;
							Particle* p2 = c->begin;
							Particle* p3 = p2 + 1;
							var pEnd = c->begin + c->length;
							var lmd2 = aglLmtLmdPtr0 + p2->index;
							for (; p3!=pEnd;) {

								// 回転する元方向と先方向を計算する処理
								static (float3 fromDir, float3 toDir) getFromToDir(
									Particle* src, Particle* dst, Quaternion q
								) {
									var from = dst->defaultL2W.c3.xyz - src->defaultL2W.c3.xyz;
									var to = dst->col.pos - src->col.pos;
									return ( mul(q, from), to );
								}

								// 拘束条件を適応
								float3 from, to;
								if (p1 != null) {
									(from, to) = getFromToDir(p2,p3,q2);
									var constraint = new Constraint_Angle{
										parent = p1,
										self = p2,
										child = p3,
										compliance = p2->angleCompliance,
										defChildPos = from + p2->col.pos
									};
									*lmd2 += constraint.solve(sqDt, *lmd2);
								}

								// 位置が変わったので、再度姿勢を計算
								if (p0 != null) {
									(from, to) = getFromToDir(p0,p1,q0);
									q0 = q1 = q2 =
										mul( Math8.fromToRotation(from, to), q0 );
								}
								if (p1 != null) {
									(from, to) = getFromToDir(p1,p2,q1);
									q1 = q2 =
										mul( Math8.fromToRotation(from, to), q1 );
								}
								(from, to) = getFromToDir(p2,p3,q2);
								p2->dWRot = q2 =
//									mul( Math8.fromToRotation(from, to), q2 );
									mul( Math8.fromToRotation(from, to, p2->maxDRotAngle), q2 );

//								// 角度制限を位置に反映する
//								p1->col.pos = p0->col.pos +
//									mul( q, from ) * ( length(to) / length(from) );

								// 法線を更新する
								var nml2 = mul( q2, p2->defaultWNml );
								var nml3 = mul( q2, p3->defaultWNml );
								p2->wNml = p2==c->begin ? nml2 : normalize(p2->wNml + nml2);
								p3->wNml = nml3;

								p0=p1; p1=p2; p2=p3; ++p3; ++lmd2;
							}
						}
					}

					// コライダとの衝突解決
					static bool solveCollider<T>(
						Common.Collider.Collider_Sphere* s,
						NativeArray<T> colliders
					) where T : struct, Common.Collider.ICollider {
						if (!colliders.IsCreated) return false;

						// 衝突を検知して、衝突がない位置まで引き離した時の位置を計算する
						float3 colN;
						float colDepth;
						var isCol = false;
						for (int i=0; i<colliders.Length; ++i) {
							if ( !colliders[i].solve(s, &colN, &colDepth) ) continue;
							s->pos += colN * colDepth;
							isCol = true;
						}

						return isCol;
					}
					{
						var lambda = cldCstLmdPtr0;
//						var compliance = i==iterationNum ? 0 : 1e-10f;
						var compliance = 1e-10f;
						for (var p=ptclPtr0; p!=ptclPtrEnd; ++p,++lambda) {
							if (p->invM == 0) continue;

							var colS = p->col;
							var isCol = false;
							isCol |= solveCollider(&colS, colliders.spheres);
							isCol |= solveCollider(&colS, colliders.capsules);
							isCol |= solveCollider(&colS, colliders.boxes);
							isCol |= solveCollider(&colS, colliders.planes);

							// 何かしらに衝突している場合は、引き離し用拘束条件を適応
							if (isCol) {
								var dPos = colS.pos - p->col.pos;
								var dPosLen = length(dPos);
								var dPosN = dPos / (dPosLen + 0.0000001f);

								var cstr = new Constraint_MinDistN{
									compliance = compliance,
									src = p->col.pos,
									n = dPosN,
									tgt = p,
									minDist = dPosLen,
								};
								*lambda += cstr.solve( sqDt, *lambda );
							} else {
								*lambda = 0;
							}
						}
					}

					// その他の拘束条件を適応する。
					static void solveConstraints<T>(float sqDt, ref float* lambda, NativeArray<T> constraints)
					where T : struct, IConstraint {
						if (!constraints.IsCreated) return;
						for (int i=0; i<constraints.Length; ++i,++lambda)
							*lambda += constraints[i].solve( sqDt, *lambda );
					}
					{
						var lambda = lmdsPtr0;
						solveConstraints(sqDt, ref lambda, _constraints.distance);
						solveConstraints(sqDt, ref lambda, _constraints.axis);
					}
				}

				cldCstLmd.Dispose();
				aglLmtLmd.Dispose();
			}

			// 速度の保存
			for (var p=ptclPtr0; p!=ptclPtrEnd; ++p)
				p->v = (p->col.pos - p->v) / dt;
		}

		// シミュレーション結果をボーンにフィードバックする
		// TODO : particlesChainを使用するようにする
		public void applyToBone( Controller.ParticleMng[] mngParticles ) {
			var ptclPtr0 = (Particle*)_particles.GetUnsafePtr();
			for (int i=0; i<_particles.Length; ++i) {

				var j=mngParticles[i];
				if ( j.parent != null ) continue;

				for (j=j.child; j!=null; j=j.child) {
					var ptcl = ptclPtr0 + j.idx;

					j.trans.parent.localRotation = Unity.Mathematics.quaternion.identity;

					// 回転する元方向と先方向
					var from = mul( j.defaultParentRot, j.trans.localPosition );
					var to = mul(
						j.trans.parent.worldToLocalMatrix,
						float4( ptcl->col.pos, 1 )
					).xyz;
					var q = Math8.fromToRotation( from, to );

					// 初期姿勢を反映
					q = mul(q, j.defaultParentRot);

					j.trans.parent.localRotation = q;
//					j.trans.position = ptcl->col.pos;
//					ptcl->col.pos = j.trans.position;
				}
			}
		}

	#if UNITY_EDITOR
		internal float3 DEBUG_getPos(int idx) {
			if (!_particles.IsCreated || _particles.Length<=idx) return default;
			return _particles[idx].col.pos;
		}
		internal float3 DEBUG_getV(int idx) {
			if (!_particles.IsCreated || _particles.Length<=idx) return default;
			return _particles[idx].v;
		}
		internal float3 DEBUG_getNml(int idx) {
			if (!_particles.IsCreated || _particles.Length<=idx) return default;
			return _particles[idx].wNml;
		}
	#endif


		// ----------------------------------- private/protected メンバ -------------------------------

		const float MinimumM = 0.00000001f;
		NativeArray<Particle> _particles;
		NativeArray<ParticleChain> _particleChains;
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
