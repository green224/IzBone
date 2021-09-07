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


	/** デフォルト姿勢でのL2Wを転送する処理 */
	[BurstCompile]
	struct MngTrans2ECSJob : IJobParallelForTransform
	{
		[ReadOnly] public NativeArray<Entity> entities;
		[ReadOnly] public ComponentDataFromEntity<Ptcl_Parent> parents;
		[ReadOnly] public ComponentDataFromEntity<Ptcl_Root> roots;
		[ReadOnly] public ComponentDataFromEntity<Root_WithAnimation> withAnims;

		[NativeDisableParallelForRestriction]
		[WriteOnly] public ComponentDataFromEntity<Ptcl_DefaultL2W> defaultL2Ws;
		[NativeDisableParallelForRestriction]
		[WriteOnly] public ComponentDataFromEntity<Ptcl_DefaultL2P> defaultL2Ps;

		public void Execute(int index, TransformAccess transform)
		{
			var entity = entities[index];
			var withAnim = withAnims[ roots[entity].value ];

			// デフォルト姿勢を毎フレーム初期化する必要がある場合は、ここで初期化
			if (withAnim.value)
				defaultL2Ps[entity] = new Ptcl_DefaultL2P(transform);

			// 一番上のパーティクルに対してのみ、TransformのL2Wを転送する
			if (parents[entity].value != null)
				defaultL2Ws[entity] = new Ptcl_DefaultL2W{value = transform.localToWorldMatrix};
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

		// 追加・削除されたAuthの情報をECSへ反映させる
		_entityReg.apply(EntityManager);


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
			}).WithoutBurst().Run();
		}


		{// デフォルト姿勢でのL2Wを計算しておく

			// ルートのL2WをECSへ転送する
			var etp = _entityReg.etPacks;
			if (etp.Length != 0) {
				Dependency = new MngTrans2ECSJob{
					entities = etp.Entities,
					parents = GetComponentDataFromEntity<Ptcl_Parent>(true),
					defaultL2Ws = GetComponentDataFromEntity<Ptcl_DefaultL2W>(false),
				}.Schedule( etp.Transforms, Dependency );
			}

			// ルート以外のL2Wを計算しておく
			Dependency = Entities.ForEach((Entity entity)=>{
				// これは上から順番に行う必要があるので、
				// RootごとにRootから順番にParticleをたどって更新する
				for (; entity!=null; entity=GetComponent<Ptcl_Next>(entity).value) {
					var parent = GetComponent<Ptcl_Parent>(entity).value;
					if (parent == null) continue;

					SetComponent(
						entity,
						new Ptcl_DefaultL2W{
							value = mul(
								GetComponent<Ptcl_DefaultL2W>(parent).value,
								GetComponent<Ptcl_DefaultL2P>(entity).l2p
							)
						}
					);
				}
			}).WithAll<Root>().Schedule( Dependency );
		}


		// 空気抵抗関係の値を事前計算しておく
		Dependency = Entities.ForEach((
			Entity entity,
			ref Root_Air air
		)=>{
			var airResRateIntegral =
				HalfLifeDragAttribute.evaluateIntegral(air.airDrag, deltaTime);

			air.winSpdIntegral = air.winSpd * (deltaTime - airResRateIntegral);
			air.airResRateIntegral = airResRateIntegral;
		}).Schedule( Dependency );


		// 質点の位置を更新
		Dependency = Entities.ForEach((
			Entity entity
		)=>{
			var invM = GetComponent<Ptcl_InvM>(entity).value;
			var sphere = GetComponent<Ptcl_Sphere>(entity).value;
			var defL2W = GetComponent<Ptcl_DefaultL2W>(entity).value;

			if (invM == 0) {
				sphere.pos = defL2W.c3.xyz;
			} else {
				var v = GetComponent<Ptcl_V>(entity).value;
				var restoreHL = GetComponent<Ptcl_RestoreHL>(entity).value;
				var root = GetComponent<Ptcl_Root>(entity).value;
				var maxSpeed = GetComponent<Root_MaxSpd>(root).value;
				var g = GetComponent<Root_G>(root).value;
				var air = GetComponent<Root_Air>(root);
						
				// 速度制限を適応
				v = clamp(v, -maxSpeed, maxSpeed);

				// 更新前の位置をvに入れておく。これは後で参照するための一時的なキャッシュ用
				SetComponent(entity, new Ptcl_V{value=sphere.pos});

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
				sphere.pos +=
					(v + g*deltaTime) * air.airResRateIntegral +
					air.winSpdIntegral; // : windSpeed * (dt - airResRateIntegral)

				// 初期位置に戻すようなフェードを掛ける
				sphere.pos = lerp(
					defL2W.c3.xyz,
					sphere.pos,
					HalfLifeDragAttribute.evaluate( restoreHL, deltaTime )
				);
			}

			SetComponent(entity, new Ptcl_Sphere{value=sphere});
		}).WithAll<Ptcl>().Schedule( Dependency );



		// λを初期化
		Dependency = Entities.ForEach((Entity entity)=>{
			SetComponent(entity, new Ptcl_CldCstLmd());
			SetComponent(entity, new Ptcl_AglLmtLmd());
			SetComponent(entity, new Ptcl_MvblRngLmd());
		}).WithAll<Ptcl>().Schedule( Dependency );
		Dependency = Entities.ForEach((Entity entity)=>{
			SetComponent(entity, new Cstr_Lmd());
		}).WithAll<DistCstr>().Schedule( Dependency );


		// XPBDによるフィッティング処理ループ
		var sqDt = deltaTime*deltaTime/ITERATION_NUM/ITERATION_NUM;
		for (int i=0; i<ITERATION_NUM; ++i) {
				
			// 角度制限の拘束条件を解決
			var em = EntityManager;
			Dependency = Entities.ForEach((Entity entity)=>{
				// これは上から順番に行う必要があるので、
				// RootごとにRootから順番にParticleをたどって更新する
				for (var p3=entity; p3!=null; p3=GetComponent<Ptcl_Next>(p3).value) {

					var p2 = GetComponent<Ptcl_Parent>(p3).value;
					if (p2 == null) continue;
					var p1 = GetComponent<Ptcl_Parent>(p2).value;
					var p0 = p1==null ? default : GetComponent<Ptcl_Parent>(p1).value;
					var p00 = p0==null ? default : GetComponent<Ptcl_Parent>(p0).value;

					var spr0 = p0==null ? default : GetComponent<Ptcl_Sphere>(p0).value;
					var spr1 = p1==null ? default : GetComponent<Ptcl_Sphere>(p1).value;
					var spr2 = GetComponent<Ptcl_Sphere>(p2).value;
					var spr3 = GetComponent<Ptcl_Sphere>(p3).value;

					var defL2W0 = p0==null ? default : GetComponent<Ptcl_DefaultL2W>(p0).value;
					var defL2W1 = p1==null ? default : GetComponent<Ptcl_DefaultL2W>(p1).value;
					var defL2W2 = GetComponent<Ptcl_DefaultL2W>(p2).value;
					var defL2W3 = GetComponent<Ptcl_DefaultL2W>(p3).value;

					// 回転する元方向と先方向を計算する処理
					static void getFromToDir(
						IzBCollider.RawCollider.Sphere srcSphere,
						IzBCollider.RawCollider.Sphere dstSphere,
						in float4x4 srcDefL2W, in float4x4 dstDefL2W,
						quaternion q,
						out float3 fromDir,
						out float3 toDir
					) {
						var from = dstDefL2W.c3.xyz - srcDefL2W.c3.xyz;
						var to = dstSphere.pos - srcSphere.pos;
						fromDir = mul(q, from);
						toDir = to;
					}

					// 拘束条件を適応
					float3 from, to;
					if (p1 != null) {
						getFromToDir(
							spr2, spr3,
							defL2W2, defL2W3,
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
					if (p0 != null) {
						var q0 = p00==null ? initQ : GetComponent<Ptcl_DWRot>(p00).value;
						getFromToDir(spr0, spr1, defL2W0, defL2W1, q0, out from, out to);
						SetComponent( p0,
							new Ptcl_DWRot{value = mul( Math8.fromToRotation(from, to), q0 )}
						);
					}
					if (p1 != null) {
						var q1 = p0==null ? initQ : GetComponent<Ptcl_DWRot>(p0).value;
						getFromToDir(spr1, spr2, defL2W1, defL2W2, q1, out from, out to);
						SetComponent( p1,
							new Ptcl_DWRot{value = mul( Math8.fromToRotation(from, to), q1 )}
						);
					}
					var q2 = p1==null ? initQ : GetComponent<Ptcl_DWRot>(p1).value;
					getFromToDir(spr2, spr3, defL2W2, defL2W3, q2, out from, out to);
					SetComponent( p2,
						new Ptcl_DWRot{value = mul( Math8.fromToRotation(from, to), q2 )}
					);
				}
			}).WithAll<Root>().Schedule( Dependency );

		}
	}

}
}
