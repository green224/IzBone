#if !USE_ECS

using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;


namespace IzBone.PhysCloth.Core {
	using Common;
	using Common.Field;

	/** シミュレーション系本体 */
	public unsafe sealed class World : IDisposable
	{
		// --------------------------------------- publicメンバ -------------------------------------

		public float3 g = float3(0,-1,0);		// 重力加速度
		public float3 windSpeed = default;		// 風速
		public HalfLife airHL = 0.1f;			// 空気抵抗による半減期。これは計算負荷削減のために、一定
		public float maxSpeed = 100;			// 最大速度

		public World(
			Authoring.ParticleMng[] mngParticles,
			Authoring.ConstraintMng[] mngConstraints
		) {
			// particlesを生成。
			// particlesは、Particleをボーンツリーの上から下に向けて並ぶようにした配列にしておくこと。
			// これにより、particlesの順番にL2Wを更新していくと、更新により親のL2Wを再計算する必要なく
			// ストレートに一巡で計算を終えることができる。
			_particles = new NativeArray<Particle>(mngParticles.Length, Allocator.Persistent);
			for (int i=0; i<mngParticles.Length; ++i) {
				var mp = mngParticles[i];
				_particles[i] = new Particle(i, mp.parent?.idx??-1, mp.getTailWPos());
			}

			// パラメータを同期
			syncWithManage(mngParticles, mngConstraints);
		}

		/** 各種パラメータをマネージ空間のものと同期する。これはEditorで実行中にパラメータが変更された際に呼ぶ */
		public void syncWithManage(
			Authoring.ParticleMng[] mngParticles,
			Authoring.ConstraintMng[] mngConstraints
		) {
			// particlesを更新
			var ptclPtr0 = (Particle*)_particles.GetUnsafePtr();
			var ptclPtrEnd = ptclPtr0 + _particles.Length;
			int i=0;
			for (var p=ptclPtr0; p!=ptclPtrEnd; ++p,++i) {
				var m = mngParticles[i];
				p->syncParams(
					m.m, m.r, radians(m.maxAngle),
					m.angleCompliance,
					m.restoreHL,
					m.maxMovableRange
				);
			}

			// constraintsを再生成
			_constraints.Dispose();
			_constraints = new Constraint.Constraints(mngConstraints, _particles);
		}

		/** 破棄する。必ず最後に呼ぶこと */
		public void Dispose() {
			_particles.Dispose();
			_constraints.Dispose();
		}

		/** 更新する。dtに0は入れない事 */
		public void update(
			float dt,
			int iterationNum,
			Authoring.ParticleMng[] mngParticles,
			IzBCollider.Colliders colliders
		) {
			// 各種バッファのポインタを取得しておく
			var ptclPtr0 = (Particle*)_particles.GetUnsafePtr();
			var ptclPtrEnd = ptclPtr0 + _particles.Length;

			// イテレーションが0回の場合は位置キャッシュだけ更新する
			if (iterationNum == 0) {
				int i = 0;
				for (var p=ptclPtr0; p!=ptclPtrEnd; ++p,++i) {
					var lastPos = p->col.pos;
					p->col.pos = mngParticles[i].getTailWPos();
					p->v = (p->col.pos - lastPos) / dt;
				}
				return;
			}
			
			{// デフォルト姿勢でのL2Wを計算しておく
				int i = 0;
				for (var p=ptclPtr0; p!=ptclPtrEnd; ++p,++i) {
					if ( mngParticles[i].transHead == null ) {
						var transTail = mngParticles[i].transTail[0];
						p->defaultHeadL2W = transTail.parent.localToWorldMatrix;
						p->defaultTailWPos = transTail.position;
					} else {
						p->defaultHeadL2W = mul(
							ptclPtr0[p->parentIdx].defaultHeadL2W,
							mngParticles[i].defaultHeadL2P
						);
						p->defaultTailWPos = Math8.trans(
							p->defaultHeadL2W,
							mngParticles[i].defaultTailLPos
						);
					}
				}
			}

			// 空気抵抗の値を計算
			var airResRate = HalfLifeDragAttribute.evaluate( airHL, dt );
			var airResRateIntegral = HalfLifeDragAttribute.evaluateIntegral( airHL, dt );

			// 質点の位置を更新
			for (var p=ptclPtr0; p!=ptclPtrEnd; ++p) {
				if (p->invM < MinimumM) {
					var mngP = mngParticles[p->index];
					p->col.pos = p->defaultTailWPos;
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
						p->defaultTailWPos,
						p->col.pos,
						HalfLifeDragAttribute.evaluate( p->restoreHL, dt )
					);
				}
			}

			{// XPBDによるフィッティング処理

				// λを初期化
				var cldCstLmd = new NativeArray<float>(_particles.Length, Allocator.Temp);
				var cldCstLmdPtr0 = (float*)cldCstLmd.GetUnsafePtr();
				var aglLmtLmd = new NativeArray<float>(_particles.Length*2, Allocator.Temp);
				var aglLmtLmdPtr0 = (float*)aglLmtLmd.GetUnsafePtr();
				var mvblRngLmd = new NativeArray<float>(_particles.Length, Allocator.Temp);
				var mvblRngLmdPtr0 = (float*)mvblRngLmd.GetUnsafePtr();
				var otherCstrLmd = new NativeArray<float>(_constraints.TotalLength, Allocator.Temp);
				var otherCstrLmdPtr0 = (float*)otherCstrLmd.GetUnsafePtr();

				// フィッティングループ
				var sqDt = dt*dt/iterationNum/iterationNum;
				for (int i=0; i<iterationNum; ++i) {

					// 角度制限の拘束条件を解決
					var lmd2 = aglLmtLmdPtr0;
					for (var p3=ptclPtr0; p3!=ptclPtrEnd; ++p3,++lmd2) {

						static Particle* getPtcl(Particle* ptr0, int idx)
							=> idx==-1 ? null : ptr0 + idx;
						static Particle* getParentPtcl(Particle* ptr0, Particle* tgt)
							=> tgt==null ? null : getPtcl(ptr0, tgt->parentIdx);
						static quaternion getPtclDWRot(Particle* tgt)
							=> tgt==null ? quaternion(0,0,0,1) : tgt->dWRot;

						var p2 = getParentPtcl(ptclPtr0, p3);
						if (p2 == null) continue;
						var p1 = getParentPtcl(ptclPtr0, p2);
						var p0 = getParentPtcl(ptclPtr0, p1);

						// 回転する元方向と先方向を計算する処理
						static (float3 fromDir, float3 toDir) getFromToDir(
							Particle* src, Particle* dst, Quaternion q
						) {
							var from = dst->defaultTailWPos - src->defaultTailWPos;
							var to = dst->col.pos - src->col.pos;
							return ( mul(q, from), to );
						}

						// 拘束条件を適応
						float3 from, to;
						if (p1 != null) {
							(from, to) = getFromToDir(p2, p3, p1->dWRot);
							var constraint = new Constraint.AngleWithLimit{
								aglCstr = new Constraint.Angle{
									pos0 = p1->col.pos,
									pos1 = p2->col.pos,
									pos2 = p3->col.pos,
									invM0 = p1->invM,
									invM1 = p2->invM,
									invM2 = p3->invM,
									defChildPos = from + p2->col.pos
								},
								compliance_nutral = p2->angleCompliance,
								compliance_limit = 0.0001f,
								limitAngle = p2->maxDRotAngle,
							};
							if (constraint.aglCstr.isValid()) {
								var l0 = *lmd2; var l1 = *(lmd2+1);
								constraint.solve(sqDt, ref l0, ref l1);
								*lmd2 = l0; *(lmd2+1) = l1;
								p1->col.pos = constraint.aglCstr.pos0;
								p2->col.pos = constraint.aglCstr.pos1;
								p3->col.pos = constraint.aglCstr.pos2;
							}
/*							var constraint = new Constraint.Angle{
								parent = p1,
								compliance = p2->angleCompliance,
								self = p2,
								child = p3,
								defChildPos = from + p2->col.pos
							};
							*lmd2 += constraint.solve(sqDt, *lmd2);
*/						}

						// 位置が変わったので、再度姿勢を計算
						if (p0 != null) {
							var p00 = getParentPtcl(ptclPtr0, p0);
							var q0 = getPtclDWRot(p00);
							(from, to) = getFromToDir(p0, p1, q0);
							p0->dWRot =
								mul( Math8.fromToRotation(from, to), q0 );
						}
						if (p1 != null) {
							var q1 = getPtclDWRot(p0);
							(from, to) = getFromToDir(p1, p2, q1);
							p1->dWRot =
								mul( Math8.fromToRotation(from, to), q1 );
						}
						var q2 = getPtclDWRot(p1);
						(from, to) = getFromToDir(p2, p3, q2);
						p2->dWRot =
							mul( Math8.fromToRotation(from, to), q2 );
					}

					{// デフォルト位置からの移動可能距離での拘束条件を解決
						var lambda = mvblRngLmdPtr0;
						var compliance = 1e-10f;
						for (var p=ptclPtr0; p!=ptclPtrEnd; ++p,++lambda) {
							if (p->invM < MinimumM || p->maxMovableRange < 0) continue;
							var cstr = new Constraint.MaxDistance{
								compliance = compliance,
								srcPos = p->defaultTailWPos,
								pos = p->col.pos,
								invM = p->invM,
								maxLen = p->maxMovableRange,
							};
							*lambda += cstr.solve(sqDt, *lambda);
							p->col.pos = cstr.pos;
						}
					}

					{// コライダとの衝突解決
						static bool solveCollider<T>(
							IzBCollider.RawCollider.Sphere* s,
							NativeArray<T> colliders
						) where T : struct, IzBCollider.RawCollider.ICollider {
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

								var cstr = new Constraint.MinDistN{
									compliance = compliance,
									srcPos = p->col.pos,
									n = dPosN,
									pos = p->col.pos,
									invM = p->invM,
									minDist = dPosLen,
								};
								*lambda += cstr.solve( sqDt, *lambda );
								p->col.pos = cstr.pos;
							} else {
								*lambda = 0;
							}
						}
					}

					{// その他の拘束条件を解決
						static void solveConstraints<T>(float sqDt, ref float* lambda, NativeArray<T> constraints)
						where T : struct, Constraint.IConstraint {
							if (!constraints.IsCreated) return;
							for (int i=0; i<constraints.Length; ++i,++lambda)
								*lambda += constraints[i].solve( sqDt, *lambda );
						}
						var lambda = otherCstrLmdPtr0;
						solveConstraints(sqDt, ref lambda, _constraints.distance);
						solveConstraints(sqDt, ref lambda, _constraints.maxDistance);
						solveConstraints(sqDt, ref lambda, _constraints.axis);
					}
				}

				cldCstLmd.Dispose();
				aglLmtLmd.Dispose();
				mvblRngLmd.Dispose();
				otherCstrLmd.Dispose();
			}

			// 速度の保存
			for (var p=ptclPtr0; p!=ptclPtrEnd; ++p)
				p->v = (p->col.pos - p->v) / dt;
		}

		// シミュレーション結果をボーンにフィードバックする
		public void applyToBone( Authoring.ParticleMng[] mngParticles ) {
			var ptclPtr0 = (Particle*)_particles.GetUnsafePtr();
			var ptclPtrEnd = ptclPtr0 + _particles.Length;
			int i = 0;
			for (var p=ptclPtr0; p!=ptclPtrEnd; ++p,++i) {

				if (p->parentIdx == -1) continue;

				var j = mngParticles[i];
				if (j.transHead == null) continue;
				
				var parentL2W = (float4x4)j.transHead.parent.localToWorldMatrix;
				var parentW2L = (float4x4)j.transHead.parent.worldToLocalMatrix;

				// 回転する元方向と先方向
				var defTailPPos = Math8.trans( j.defaultHeadL2P, j.defaultTailLPos );
				var curTailPPos = Math8.trans( parentW2L, p->col.pos );
				var defHeadPPos = j.defaultHeadL2P.c3.xyz;

				// 最大角度制限は制約条件のみだとどうしても完璧にはならず、
				// コンプライアンス値をきつくし過ぎると暴走するので、
				// 制約条件で緩く制御した上で、ここで強制的にクリッピングする。
				var q = Math8.fromToRotation(
					defTailPPos - defHeadPPos,
					curTailPPos - defHeadPPos,
					p->maxDRotAngle
				);

				// 初期姿勢を反映
				q = mul(q, j.defaultHeadRot);

				j.transHead.localRotation = q;
//				j.transHead.localPosition = normalizesafe(j.defaultTailLPos)
//					* ( length(curTailPPos) - length(defTailPPos) );

				// 最大角度制限を反映させたので、パーティクルへ変更をフィードバックする
				p->col.pos = j.transHead.localToWorldMatrix.MultiplyPoint(j.defaultTailLPos);
			}
		}

	#if UNITY_EDITOR
		internal Particle DEBUG_getPtcl(int idx) {
			if (!_particles.IsCreated || _particles.Length<=idx) return default;
			return _particles[idx];
		}
	#endif


		// ----------------------------------- private/protected メンバ -------------------------------

		const float MinimumM = 0.00000001f;
		NativeArray<Particle> _particles;
		Constraint.Constraints _constraints;

		~World() {
			if ( _particles.IsCreated ) {
				Debug.LogError("World is not disposed");
				Dispose();
			}
		}


		// --------------------------------------------------------------------------------------------
	}

}


#endif