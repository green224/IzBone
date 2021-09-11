//#define WITH_DEBUG
using System;
using UnityEngine.Jobs;
using Unity.Jobs;

using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using System.Runtime.CompilerServices;



namespace IzBone.PhysCloth.Core {
using Common;
using Common.Field;

[UpdateInGroup(typeof(IzBoneSystemGroup))]
[UpdateAfter(typeof(IzBCollider.Core.IzBColliderSystem))]
[AlwaysUpdateSystem]
public sealed class IzBPhysClothSystem : SystemBase {

	// フィッティングループのイテレーションカウント
	const int ITERATION_NUM = 5;

	// Authoringを登録・登録解除する処理
	internal void register(Authoring.BaseAuthoring auth, EntityRegisterer.RegLink regLink)
		=> _entityReg.register(auth, regLink);
	internal void unregister(Authoring.BaseAuthoring auth, EntityRegisterer.RegLink regLink)
		=> _entityReg.unregister(auth, regLink);
	internal void resetParameters(EntityRegisterer.RegLink regLink)
		=> _entityReg.resetParameters(regLink);
	EntityRegisterer _entityReg;

	/** 指定のAuthの物理状態をリセットする */
	internal void reset(EntityRegisterer.RegLink regLink) {
		var etp = _entityReg.etPacks;
		for (int i=0; i<regLink.etpIdxs.Count; ++i) {
			var etpIdx = regLink.etpIdxs[i];
			var e = etp.Entities[ etpIdx ];
			var t = etp.Transforms[ etpIdx ];

			if (!HasComponent<Ptcl>(e)) continue;

			var sphere = GetComponent<Ptcl_Sphere>(e);
			var defTailLPos = GetComponent<Ptcl_DefaultTailLPos>(e).value;
			sphere.value.pos = t.localToWorldMatrix.MultiplyPoint(defTailLPos);
			
			SetComponent(e, new Ptcl_V());
			SetComponent(e, sphere);
		}
	}


	/** デフォルト姿勢でのL2Wを転送する処理 */
	[BurstCompile]
	struct MngTrans2ECSJob : IJobParallelForTransform
	{
		[ReadOnly] public NativeArray<Entity> entities;
		[ReadOnly] public ComponentDataFromEntity<Ptcl_InvM> invMs;
		[ReadOnly] public ComponentDataFromEntity<Ptcl_Root> roots;
		[ReadOnly] public ComponentDataFromEntity<Root_WithAnimation> withAnims;

		[NativeDisableParallelForRestriction]
		[WriteOnly] public ComponentDataFromEntity<Ptcl_DefaultHeadL2W> defHeadL2Ws;
		[NativeDisableParallelForRestriction]
		[WriteOnly] public ComponentDataFromEntity<Ptcl_DefaultHeadL2P> defHeadL2Ps;
// defTailLPosはWithAnimがTrueのときでも毎フレームは更新しない。
//		[NativeDisableParallelForRestriction]
//		[WriteOnly] public ComponentDataFromEntity<Ptcl_DefaultTailLPos> defTailLPoss;
		[ReadOnly] public ComponentDataFromEntity<Ptcl_DefaultTailLPos> defTailLPoss;
		[NativeDisableParallelForRestriction]
		[WriteOnly] public ComponentDataFromEntity<Ptcl_DefaultTailWPos> defTailWPoss;
		[NativeDisableParallelForRestriction]
		[WriteOnly] public ComponentDataFromEntity<Ptcl_CurHeadTrans> curHeadTranss;

		public void Execute(int index, TransformAccess transform) {
			var entity = entities[index];
			var withAnim = withAnims[ roots[entity].value ];

			// デフォルト姿勢を毎フレーム初期化する必要がある場合は、ここで初期化
			if (withAnim.value) {
				defHeadL2Ps[entity] = new Ptcl_DefaultHeadL2P(transform);
//				defTailLPoss[entity] = 
			}

			// 現在のTransformをすべてのParticleに対して転送
			curHeadTranss[entity] = new Ptcl_CurHeadTrans{
				l2w = transform.localToWorldMatrix,
				w2l = transform.worldToLocalMatrix,
				lPos = transform.localPosition,
				lRot = transform.localRotation,
			};

			// 固定パーティクルに対しては、DefaultL2Wを現在値に更新する
			if (invMs[entity].value == 0) {
				var headL2W = transform.localToWorldMatrix;
				var tailLPos = defTailLPoss[entity].value;
				defHeadL2Ws[entity] = new Ptcl_DefaultHeadL2W{value = headL2W};
				defTailWPoss[entity] =
					new Ptcl_DefaultTailWPos{value = Math8.trans(headL2W, tailLPos)};
			}
		}
		public void Execute(int index, UnityEngine.Transform transform) {
			var entity = entities[index];
			var withAnim = withAnims[ roots[entity].value ];

			// デフォルト姿勢を毎フレーム初期化する必要がある場合は、ここで初期化
			if (withAnim.value) {
				defHeadL2Ps[entity] = new Ptcl_DefaultHeadL2P(transform);
//				defTailLPoss[entity] = 
			}

			// 現在のTransformをすべてのParticleに対して転送
			curHeadTranss[entity] = new Ptcl_CurHeadTrans{
				l2w = transform.localToWorldMatrix,
				w2l = transform.worldToLocalMatrix,
				lPos = transform.localPosition,
				lRot = transform.localRotation,
			};

			// 固定パーティクルに対しては、DefaultL2Wを現在値に更新する
			if (invMs[entity].value == 0) {
				var headL2W = transform.localToWorldMatrix;
				var tailLPos = defTailLPoss[entity].value;
//UnityEngine.Debug.Log("aaa:"+transform.name);
//UnityEngine.Debug.Log(headL2W);
//UnityEngine.Debug.Log(tailLPos);
				defHeadL2Ws[entity] = new Ptcl_DefaultHeadL2W{value = headL2W};
				defTailWPoss[entity] =
					new Ptcl_DefaultTailWPos{value = Math8.trans(headL2W, tailLPos)};
			}
		}
	}


	/** シミュレーション結果をボーンにフィードバックする */
	[BurstCompile]
	struct ApplyToBoneJob : IJobParallelForTransform
	{
		[ReadOnly] public NativeArray<Entity> entities;
		[ReadOnly] public ComponentDataFromEntity<Ptcl_CurHeadTrans> curHeadTranss;
		[ReadOnly] public ComponentDataFromEntity<Ptcl_InvM> invMs;

		public void Execute(int index, TransformAccess transform)
		{
			var entity = entities[index];
			var curTrans = curHeadTranss[entity];
			var invM = invMs[entity].value;
			if (invM != 0) {
				transform.localPosition = curTrans.lPos;
				transform.localRotation = curTrans.lRot;
			}
		}
	}


	protected override void OnCreate() {
		_entityReg = new EntityRegisterer();
	}

	protected override void OnDestroy() {
		_entityReg.Dispose();
	}

	override protected void OnUpdate() {
//		var deltaTime = World.GetOrCreateSystem<Time8.TimeSystem>().DeltaTime;
		var deltaTime = Time.DeltaTime;
		if (deltaTime < 0.00001f) return;	// とりあえずdt=0のときはやらないでおく。TODO: あとで何とかする

		// 追加・削除されたAuthの情報をECSへ反映させる
		_entityReg.apply(EntityManager);
		var etp = _entityReg.etPacks;


	#if UNITY_EDITOR
		{// デバッグ用のバッファを更新
			Entities.ForEach((
				Entity entity,
				in Ptcl_M2D ptclM2D,
				in Ptcl_V ptclV,
				in Ptcl_Sphere ptclSphere
			)=>{
				ptclM2D.auth.DEBUG_curV = ptclV.value;
				ptclM2D.auth.DEBUG_curPos = ptclSphere.value.pos;
			}).WithoutBurst().Run();
		}
	#endif

		{// マネージド空間から、毎フレーム同期する必要のあるパラメータを同期
			var defG = (float3)UnityEngine.Physics.gravity;
			Entities.ForEach((
				Entity entity,
				in Root_M2D rootM2D
			)=>{
				SetComponent(entity, new Root_UseSimulation{value = rootM2D.auth.useSimulation});
				SetComponent(entity, new Root_G{value = rootM2D.auth.g.evaluate(defG)});
				SetComponent(entity, new Root_Air{
					winSpd = rootM2D.auth.windSpeed,
					airDrag = rootM2D.auth.airDrag,
				});
				SetComponent(entity, new Root_MaxSpd{value = rootM2D.auth.maxSpeed});
				SetComponent(entity, new Root_WithAnimation{value = rootM2D.auth.withAnimation});
				var collider = rootM2D.auth._collider?.RootEntity ?? Entity.Null;
				SetComponent(entity, new Root_ColliderPack{value = collider});
			}).WithoutBurst().Run();
		}


		{// デフォルト姿勢でのL2Wを計算しておく

			// ルートのL2WをECSへ転送する
			if (etp.Length != 0) {
			#if WITH_DEBUG
				var a = new MngTrans2ECSJob{
			#else
				Dependency = new MngTrans2ECSJob{
			#endif
					entities = etp.Entities,
					invMs = GetComponentDataFromEntity<Ptcl_InvM>(true),
					roots = GetComponentDataFromEntity<Ptcl_Root>(true),
					withAnims = GetComponentDataFromEntity<Root_WithAnimation>(true),

					defHeadL2Ws = GetComponentDataFromEntity<Ptcl_DefaultHeadL2W>(false),
					defHeadL2Ps = GetComponentDataFromEntity<Ptcl_DefaultHeadL2P>(false),
					defTailLPoss = GetComponentDataFromEntity<Ptcl_DefaultTailLPos>(true),
					defTailWPoss = GetComponentDataFromEntity<Ptcl_DefaultTailWPos>(false),
					curHeadTranss = GetComponentDataFromEntity<Ptcl_CurHeadTrans>(false),
			#if WITH_DEBUG
				};
				for (int i=0; i<etp.Transforms.length; ++i) {
					a.Execute( i, etp.Transforms[i] );
				}
			#else
				}.Schedule( etp.Transforms, Dependency );
			#endif
			}

			// ルート以外のL2Wを計算しておく
		#if WITH_DEBUG
			Entities.ForEach((Entity entity)=>{
		#else
			Dependency = Entities.ForEach((Entity entity)=>{
		#endif
				// これは上から順番に行う必要があるので、
				// RootごとにRootから順番にParticleをたどって更新する
				for (; entity!=Entity.Null; entity=GetComponent<Ptcl_Next>(entity).value) {
					var invM = GetComponent<Ptcl_InvM>(entity).value;
					if (invM == 0) continue;

					var parent = GetComponent<Ptcl_Parent>(entity).value;
					var defHeadL2W = mul(
						GetComponent<Ptcl_DefaultHeadL2W>(parent).value,
						GetComponent<Ptcl_DefaultHeadL2P>(entity).l2p
					);
					var defTailLPos = GetComponent<Ptcl_DefaultTailLPos>(entity).value;
					var defTailWPos = Math8.trans(defHeadL2W, defTailLPos);
//UnityEngine.Debug.Log("bbb:");
//UnityEngine.Debug.Log(defTailWPos);
					SetComponent(entity, new Ptcl_DefaultHeadL2W{value = defHeadL2W});
					SetComponent(entity, new Ptcl_DefaultTailWPos{value = defTailWPos});
				}
		#if WITH_DEBUG
			}).WithAll<Root>().WithoutBurst().Run();
		#else
			}).WithAll<Root>().Schedule( Dependency );
		#endif
		}


		// 空気抵抗関係の値を事前計算しておく
	#if WITH_DEBUG
		Entities.ForEach((
	#else
		Dependency = Entities.ForEach((
	#endif
			Entity entity,
			ref Root_Air air
		)=>{
			var airResRateIntegral =
				HalfLifeDragAttribute.evaluateIntegral(air.airDrag, deltaTime);

			air.winSpdIntegral = air.winSpd * (deltaTime - airResRateIntegral);
			air.airResRateIntegral = airResRateIntegral;
	#if WITH_DEBUG
		}).WithoutBurst().Run();
	#else
		}).Schedule( Dependency );
	#endif


		// 質点の位置を更新
	#if WITH_DEBUG
		Entities.ForEach((
	#else
		Dependency = Entities.ForEach((
	#endif
			Entity entity,
			ref Ptcl_Sphere sphere,
			ref Ptcl_V v
	#if WITH_DEBUG
			,in Ptcl_M2D m2d
	#endif
		)=>{
			var invM = GetComponent<Ptcl_InvM>(entity).value;
			var defTailPos = GetComponent<Ptcl_DefaultTailWPos>(entity).value;

			if (invM == 0) {
				sphere.value.pos = defTailPos;
			} else {
				var restoreHL = GetComponent<Ptcl_RestoreHL>(entity).value;
				var root = GetComponent<Ptcl_Root>(entity).value;
				var maxSpeed = GetComponent<Root_MaxSpd>(root).value;
				var g = GetComponent<Root_G>(root).value;
				var air = GetComponent<Root_Air>(root);
						
				// 速度制限を適応
				var v0 = clamp(v.value, -maxSpeed, maxSpeed);

				// 更新前の位置をvに入れておく。これは後で参照するための一時的なキャッシュ用
				v.value = sphere.value.pos;

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
				sphere.value.pos +=
					(v0 + g*deltaTime) * air.airResRateIntegral +
					air.winSpdIntegral; // : windSpeed * (dt - airResRateIntegral)

				// 初期位置に戻すようなフェードを掛ける
				sphere.value.pos = lerp(
					defTailPos,
					sphere.value.pos,
					HalfLifeDragAttribute.evaluate( restoreHL, deltaTime )
				);
			}
//UnityEngine.Debug.Log("ccc:"+(m2d.auth.transHead==null?"*":m2d.auth.transHead.name));
//UnityEngine.Debug.Log(sphere.value.pos);
	#if WITH_DEBUG
		}).WithAll<Ptcl>().WithoutBurst().Run();
	#else
		}).WithAll<Ptcl>().Schedule( Dependency );
	#endif



		// λを初期化
	#if WITH_DEBUG
		Entities.ForEach((Entity entity)=>{
	#else
		Dependency = Entities.ForEach((Entity entity)=>{
	#endif
			SetComponent(entity, new Ptcl_CldCstLmd());
			SetComponent(entity, new Ptcl_AglLmtLmd());
			SetComponent(entity, new Ptcl_MvblRngLmd());
	#if WITH_DEBUG
		}).WithAll<Ptcl>().WithoutBurst().Run();
		Entities.ForEach((Entity entity)=>{
	#else
		}).WithAll<Ptcl>().Schedule( Dependency );
		Dependency = Entities.ForEach((Entity entity)=>{
	#endif
			SetComponent(entity, new Cstr_Lmd());
	#if WITH_DEBUG
		}).WithAll<DistCstr>().WithoutBurst().Run();
	#else
		}).WithAll<DistCstr>().Schedule( Dependency );
	#endif


		// XPBDによるフィッティング処理ループ
		var sqDt = deltaTime*deltaTime/ITERATION_NUM/ITERATION_NUM;
		for (int i=0; i<ITERATION_NUM; ++i) {

			// 角度制限の拘束条件を解決
			var em = EntityManager;
	#if true
		#if WITH_DEBUG
			Entities.ForEach((Entity entity)=>{
		#else
			Dependency = Entities.ForEach((Entity entity)=>{
		#endif
				// これは上から順番に行う必要があるので、
				// RootごとにRootから順番にParticleをたどって更新する
				for (var p3=entity; p3!=Entity.Null; p3=GetComponent<Ptcl_Next>(p3).value) {

					var p2 = GetComponent<Ptcl_Parent>(p3).value;
					if (p2 == Entity.Null) continue;
					var p1 = GetComponent<Ptcl_Parent>(p2).value;
					var p0 = p1==Entity.Null ? Entity.Null : GetComponent<Ptcl_Parent>(p1).value;
					var p00 = p0==Entity.Null ? Entity.Null : GetComponent<Ptcl_Parent>(p0).value;

					var spr0 = p0==Entity.Null ? default : GetComponent<Ptcl_Sphere>(p0).value;
					var spr1 = p1==Entity.Null ? default : GetComponent<Ptcl_Sphere>(p1).value;
					var spr2 = GetComponent<Ptcl_Sphere>(p2).value;
					var spr3 = GetComponent<Ptcl_Sphere>(p3).value;

					var defWPos0 = p0==Entity.Null ? default : GetComponent<Ptcl_DefaultTailWPos>(p0).value;
					var defWPos1 = p1==Entity.Null ? default : GetComponent<Ptcl_DefaultTailWPos>(p1).value;
					var defWPos2 = GetComponent<Ptcl_DefaultTailWPos>(p2).value;
					var defWPos3 = GetComponent<Ptcl_DefaultTailWPos>(p3).value;

					// 回転する元方向と先方向を計算する処理
					static void getFromToDir(
						IzBCollider.RawCollider.Sphere srcSphere,
						IzBCollider.RawCollider.Sphere dstSphere,
						float3 srcWPos, float3 dstWPos,
						quaternion q,
						out float3 fromDir,
						out float3 toDir
					) {
						var from = dstWPos - srcWPos;
						var to = dstSphere.pos - srcSphere.pos;
						fromDir = mul(q, from);
						toDir = to;
					}

					// 拘束条件を適応
					float3 from, to;
					if (p1 != Entity.Null) {
						getFromToDir(
							spr2, spr3,
							defWPos2, defWPos3,
							GetComponent<Ptcl_DWRot>(p1).value,
							out from, out to
						);
						var constraint = new Constraint.AngleWithLimit{
							aglCstr = new Constraint.Angle{
								pos0 = spr1.pos,
								pos1 = spr2.pos,
								pos2 = spr3.pos,
								invM0 = GetComponent<Ptcl_InvM>(p1).value,
								invM1 = GetComponent<Ptcl_InvM>(p2).value,
								invM2 = GetComponent<Ptcl_InvM>(p3).value,
								defChildPos = from + spr2.pos
							},
							compliance_nutral = GetComponent<Ptcl_AngleCompliance>(p2).value,
							compliance_limit = 0.0001f,
							limitAngle = GetComponent<Ptcl_MaxAngle>(p2).value,
						};
						if (constraint.aglCstr.isValid()) {
							var lmd2 = GetComponent<Ptcl_AglLmtLmd>(p2).value;
							var lmd3 = GetComponent<Ptcl_AglLmtLmd>(p3).value;
							constraint.solve(sqDt, ref lmd2, ref lmd3);
							spr1.pos = constraint.aglCstr.pos0;
							spr2.pos = constraint.aglCstr.pos1;
							spr3.pos = constraint.aglCstr.pos2;
							SetComponent(p2, new Ptcl_AglLmtLmd{value = lmd2});
							SetComponent(p3, new Ptcl_AglLmtLmd{value = lmd3});
							SetComponent(p1, new Ptcl_Sphere{value = spr1});
							SetComponent(p2, new Ptcl_Sphere{value = spr2});
							SetComponent(p3, new Ptcl_Sphere{value = spr3});
						}
/*						var constraint = new Constraint.Angle{
							parent = p1,
							compliance = p2->angleCompliance,
							self = p2,
							child = p3,
							defChildPos = from + p2->col.pos
						};
						*lmd2 += constraint.solve(sqDt, *lmd2);
*/					}

					// 位置が変わったので、再度姿勢を計算
					var initQ = quaternion(0,0,0,1);
					if (p0 != Entity.Null) {
						var q0 = p00==Entity.Null ? initQ : GetComponent<Ptcl_DWRot>(p00).value;
						getFromToDir(spr0, spr1, defWPos0, defWPos1, q0, out from, out to);
						SetComponent( p0,
							new Ptcl_DWRot{value = mul( Math8.fromToRotation(from, to), q0 )}
						);
					}
					if (p1 != Entity.Null) {
						var q1 = p0==Entity.Null ? initQ : GetComponent<Ptcl_DWRot>(p0).value;
						getFromToDir(spr1, spr2, defWPos1, defWPos2, q1, out from, out to);
						SetComponent( p1,
							new Ptcl_DWRot{value = mul( Math8.fromToRotation(from, to), q1 )}
						);
					}
					var q2 = p1==Entity.Null ? initQ : GetComponent<Ptcl_DWRot>(p1).value;
					getFromToDir(spr2, spr3, defWPos2, defWPos3, q2, out from, out to);
					SetComponent( p2,
						new Ptcl_DWRot{value = mul( Math8.fromToRotation(from, to), q2 )}
					);
				}
		#if WITH_DEBUG
			}).WithAll<Root>().WithoutBurst().Run();
		#else
			}).WithAll<Root>().Schedule( Dependency );
		#endif
	#endif



			// デフォルト位置からの移動可能距離での拘束条件を解決
			const float DefPosMovRngCompliance = 1e-10f;
		#if WITH_DEBUG
			Entities.ForEach((
		#else
			Dependency = Entities.ForEach((
		#endif
				Entity entity,
				ref Ptcl_Sphere sphere,
				ref Ptcl_MvblRngLmd lambda
			)=>{
				// 固定Particleに対しては何もする必要なし
				var invM = GetComponent<Ptcl_InvM>(entity).value;
				if (invM == 0) return;

				var maxMovableRange = GetComponent<Ptcl_MaxMovableRange>(entity).value;
				if (maxMovableRange < 0) return;

				var cstr = new Constraint.MaxDistance{
					compliance = DefPosMovRngCompliance,
					srcPos = GetComponent<Ptcl_DefaultTailWPos>(entity).value,
					pos = sphere.value.pos,
					invM = invM,
					maxLen = maxMovableRange,
				};

				lambda.value += cstr.solve(sqDt, lambda.value);
				sphere.value.pos = cstr.pos;

		#if WITH_DEBUG
			}).WithoutBurst().Run();
		#else
			}).Schedule( Dependency );
		#endif



			// コライダとの衝突解決
			const float ColResolveCompliance = 1e-10f;
		#if WITH_DEBUG
			Entities.ForEach((
		#else
			Dependency = Entities.ForEach((
		#endif
				Entity entity,
				ref Ptcl_Sphere sphere,
				ref Ptcl_CldCstLmd lambda
			)=>{
				var mostParent = GetComponent<Ptcl_Root>(entity).value;

				// コライダが未設定の場合は何もしない
				var colliderPack = GetComponent<Root_ColliderPack>(mostParent).value;
				if (colliderPack == Entity.Null) return;

				// 固定Particleに対しては何もする必要なし
				var invM = GetComponent<Ptcl_InvM>(entity).value;
				if (invM == 0) return;

				// コライダとの衝突解決
				var pos = sphere.value.pos;
				var isCol = false;
				for (
					var e = colliderPack;
					e != Entity.Null;
					e = GetComponent<IzBCollider.Core.Body_Next>(e).value
				) {
					var rc = GetComponent<IzBCollider.Core.Body_RawCollider>(e);
					var st = GetComponent<IzBCollider.Core.Body_ShapeType>(e).value;
					isCol |= rc.solveCollision( st, ref pos, sphere.value.r );
				}

				// 何かしらに衝突している場合は、引き離し用拘束条件を適応
				if (isCol) {
					var dPos = pos - sphere.value.pos;
					var dPosLen = length(dPos);
					var dPosN = dPos / (dPosLen + 0.0000001f);

					var cstr = new Constraint.MinDistN{
						compliance = ColResolveCompliance,
						srcPos = sphere.value.pos,
						n = dPosN,
						pos = sphere.value.pos,
						invM = invM,
						minDist = dPosLen,
					};
					lambda.value += cstr.solve( sqDt, lambda.value );
					sphere.value.pos = cstr.pos;
				} else {
					lambda.value = 0;
				}
		#if WITH_DEBUG
			}).WithoutBurst().Run();
		#else
			}).Schedule( Dependency );
		#endif



			// その他の拘束条件を解決
		#if WITH_DEBUG
			Entities.ForEach((
		#else
			Dependency = Entities.ForEach((
		#endif
				Entity entity,
				ref Cstr_Lmd lambda
			)=>{
				var tgt = GetComponent<Cstr_Target>(entity);
				var compliance = GetComponent<Cstr_Compliance>(entity).value;
				var defaultLen = GetComponent<Cstr_DefaultLen>(entity).value;

				var sphere0 = GetComponent<Ptcl_Sphere>(tgt.src);
				var sphere1 = GetComponent<Ptcl_Sphere>(tgt.dst);
				var cstr = new Constraint.Distance{
					compliance = compliance,
					pos0 = sphere0.value.pos,
					pos1 = sphere1.value.pos,
					invM0 = GetComponent<Ptcl_InvM>(tgt.src).value,
					invM1 = GetComponent<Ptcl_InvM>(tgt.dst).value,
					defLen = defaultLen,
				};

				lambda.value += cstr.solve(sqDt, lambda.value);
				sphere0.value.pos = cstr.pos0;
				sphere1.value.pos = cstr.pos1;
				SetComponent(tgt.src, sphere0);
				SetComponent(tgt.dst, sphere1);
		#if WITH_DEBUG
			}).WithoutBurst().Run();
		#else
			}).Schedule( Dependency );
		#endif
		}


		// 速度の保存
	#if WITH_DEBUG
		Entities.ForEach((
	#else
		Dependency = Entities.ForEach((
	#endif
			Entity entity,
			ref Ptcl_V v,
			in Ptcl_Sphere sphere
		)=>{
			v.value = (sphere.value.pos - v.value) / deltaTime;
	#if WITH_DEBUG
		}).WithoutBurst().Run();
	#else
		}).Schedule( Dependency );
	#endif



		// シミュレーション結果をボーンにフィードバックする
	#if WITH_DEBUG
		Entities.ForEach((
	#else
		Dependency = Entities.ForEach((
	#endif
			Entity entity
		)=>{
			// これは上から順番に行う必要があるので、
			// RootごとにRootから順番にParticleをたどって更新する
			for (; entity!=Entity.Null; entity=GetComponent<Ptcl_Next>(entity).value) {

				// 最親Particleの場合はDefaultTransformをそのまま転写
				var invM = GetComponent<Ptcl_InvM>(entity).value;
				if (invM == 0) {
//					var l2w = GetComponent<Ptcl_DefaultHeadL2W>(entity).value;
//					SetComponent(
//						entity,　new Ptcl_CurHeadTrans{l2w=l2w, w2l=inverse(l2w)}
//					);
					continue;
				}
				var parent = GetComponent<Ptcl_Parent>(entity).value;

				// 親のTransform
				var parentTrans = GetComponent<Ptcl_CurHeadTrans>(parent);

				var sphere = GetComponent<Ptcl_Sphere>(entity);
				var defHeadL2P = GetComponent<Ptcl_DefaultHeadL2P>(entity);
				var defTailLPos = GetComponent<Ptcl_DefaultTailLPos>(entity).value;

				// 回転する元方向と先方向
				var defTailPPos = Math8.trans( defHeadL2P.l2p, defTailLPos );
				var curTailPPos = Math8.trans( parentTrans.w2l, sphere.value.pos );
				var defHeadPPos = defHeadL2P.l2p.c3.xyz;

				// 最大角度制限は制約条件のみだとどうしても完璧にはならず、
				// コンプライアンス値をきつくし過ぎると暴走するので、
				// 制約条件で緩く制御した上で、ここで強制的にクリッピングする。
				var maxAngle = GetComponent<Ptcl_MaxAngle>(entity).value;
				var q = Math8.fromToRotation(
					defTailPPos - defHeadPPos,
					curTailPPos - defHeadPPos,
					maxAngle
				);

				// 初期姿勢を反映
				var curTrans = GetComponent<Ptcl_CurHeadTrans>(entity);
				curTrans.lRot = mul(q, defHeadL2P.rot);
				curTrans.l2w = mul(
					parentTrans.l2w,
					Unity.Mathematics.float4x4.TRS(curTrans.lPos, curTrans.lRot, 1)
				);
				curTrans.w2l = inverse(curTrans.l2w);
				SetComponent(entity, curTrans);

				// 最大角度制限を反映させたので、パーティクルへ変更をフィードバックする
				sphere.value.pos = Math8.trans(curTrans.l2w, defTailLPos);
				SetComponent(entity, sphere);
			}
	#if WITH_DEBUG
		}).WithAll<Root>().WithoutBurst().Run();
	#else
		}).WithAll<Root>().Schedule( Dependency );
	#endif
		if (etp.Length != 0) {
			Dependency = new ApplyToBoneJob{
				entities = etp.Entities,
				curHeadTranss = GetComponentDataFromEntity<Ptcl_CurHeadTrans>(true),
				invMs = GetComponentDataFromEntity<Ptcl_InvM>(true),
			}.Schedule( etp.Transforms, Dependency );
		}

	}


}
}
