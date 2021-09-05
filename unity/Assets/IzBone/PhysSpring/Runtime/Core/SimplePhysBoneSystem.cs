using UnityEngine.Jobs;
using Unity.Jobs;

using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using System.Runtime.CompilerServices;



namespace IzBone.PhysSpring.Core {
using Common;

[UpdateInGroup(typeof(IzBoneSystemGroup))]
[UpdateAfter(typeof(IzBCollider.Core.IzBColliderSystem))]
[AlwaysUpdateSystem]
public sealed class SimplePhysBoneSystem : SystemBase {

	// BodyAuthoringを登録・登録解除する処理
	internal void register(RootAuthoring auth, EntityRegisterer.RegLink regLink)
		=> _entityReg.register(auth, regLink);
	internal void unregister(RootAuthoring auth, EntityRegisterer.RegLink regLink)
		=> _entityReg.unregister(auth, regLink);
	internal void resetAllParameters() => _entityReg.resetAllParameters();
	EntityRegisterer _entityReg;

	/** 指定のRootAuthの物理状態をリセットする */
	internal void reset(EntityRegisterer.RegLink regLink) {
		var etp = _entityReg.etPacks;
		for (int i=0; i<regLink.etpIdxs.Count; ++i) {
			var etpIdx = regLink.etpIdxs[i];
			var e = etp.Entities[ etpIdx ];
			var t = etp.Transforms[ etpIdx ];

			var spring = GetComponent<OneSpring>(e);
			spring.spring_rot.v = 0;
			spring.spring_sft.v = 0;
			SetComponent(e, spring);

			var defState = GetComponent<DefaultState>(e);
			var childWPos = t.localToWorldMatrix.MultiplyPoint(defState.childDefPos);

			var wPosCache = GetComponent<WPosCache>(e);
			wPosCache.lastWPos = childWPos;
			SetComponent(e, wPosCache);
		}
	}

	/** ECSで得た結果の回転を、マネージドTransformに反映させる処理 */
	[BurstCompile]
	struct SpringTransUpdateJob : IJobParallelForTransform
	{
		[ReadOnly] public NativeArray<Entity> entities;
		[ReadOnly] public ComponentDataFromEntity<SpringResult> springResults;

		public void Execute(int index, TransformAccess transform)
		{
			var entity = entities[index];
			var bodyResult = springResults[entity];

			// 適応
			transform.localPosition = bodyResult.trs;
			transform.localRotation = bodyResult.rot;
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

		// マネージド空間の情報をECSへ同期する
		Entities.ForEach((
			ref DefaultState defState,
			in OneSpring_M2D springM2D
		)=>{
			var parent = springM2D.parentTrans;

			// 現在位置をデフォルト位置として再計算する必要がある場合は、
			// このタイミングで再計算を行う
			if (defState.resetDefPosAlways) {
				var child = springM2D.childTrans;

				defState.defRot = parent.localRotation;
				defState.defPos = parent.localPosition;
				defState.childDefPos = child.localPosition;
				var scl = float3(0,0,0);
				scl = parent.localScale;
				defState.childDefPosMPR = mul(defState.defRot, scl * defState.childDefPos);
			}

			// スケールだけは必ず同期する
			defState.curScale = parent.localScale;

		}).WithoutBurst().Run();
		Entities.ForEach((
			ref MostParent mostParent,
			in OneSpring_M2D springM2D
		)=>{
			// 最親のL2Wを同期
			var parent = springM2D.parentTrans;
			mostParent.ppL2W = parent.parent.localToWorldMatrix;
		}).WithoutBurst().Run();


		// シミュレーションの本更新処理
		Dependency = Entities.ForEach((
			Entity entity,
			in MostParent mostParent
		)=>{
			// 一繋ぎ分のSpringの情報をまとめて取得しておく
			var buf_spring    = new NativeArray<OneSpring>(mostParent.depth, Allocator.Temp);
			var buf_defState  = new NativeArray<DefaultState>(mostParent.depth, Allocator.Temp);
			var buf_wPosCache = new NativeArray<WPosCache>(mostParent.depth, Allocator.Temp);
			var buf_result    = new NativeArray<SpringResult>(mostParent.depth, Allocator.Temp);
			var buf_entity    = new NativeArray<Entity>(mostParent.depth, Allocator.Temp);
			{
				var e = entity;
				for (int i=0;; ++i) {
					buf_entity[i]    = e;
					buf_spring[i]    = GetComponent<OneSpring>(e);
					buf_defState[i]  = GetComponent<DefaultState>(e);
					buf_wPosCache[i] = GetComponent<WPosCache>(e);
					buf_result[i]    = GetComponent<SpringResult>(e);
					if (i == mostParent.depth-1) break;
					e = GetComponent<Child>(e).value;
				}
			}

			// 本更新処理
			var iterationNum = mostParent.iterationNum;
			var rsRate = mostParent.rsRate;
			var dt = deltaTime / iterationNum;
			for (int itr=0; itr<iterationNum; ++itr) {
				var ppL2W = mostParent.ppL2W;
				for (int i=0; i<mostParent.depth; ++i) {

					// OneSpringごとのコンポーネントを取得
					var spring = buf_spring[i];
					var defState = buf_defState[i];
					var wPosCache = buf_wPosCache[i];

					// 前フレームにキャッシュされた位置にパーティクルが移動したとして、
					// その位置でコライダとの衝突解決をしておく
					if (mostParent.colliderPack != default) {
						for (
							var e = GetComponent<IzBCollider.Core.BodiesPack>(mostParent.colliderPack).first;
							e != default;
							e = GetComponent<IzBCollider.Core.Body_Next>(e).value
						) {
							var rc = GetComponent<IzBCollider.Core.Body_RawCollider>(e);
							var st = GetComponent<IzBCollider.Core.Body_ShapeType>(e).value;
							rc.solveCollision( st, ref wPosCache.lastWPos, defState.r );
						}
					}

					// 前フレームにキャッシュされた位置を先端目標位置として、
					// 先端目標位置へ移動した結果の移動・姿勢ベクトルを得る
					float3 sftVec, rotVec;
					{
						// ワールド座標をボーンローカル座標に変換する
						var ppW2L = Math8.inverse(ppL2W);
						var tgtBPos = mulMxPos(ppW2L, wPosCache.lastWPos) - defState.defPos;

						// 移動と回転の影響割合を考慮してシミュレーション結果を得る
						var cdpMPR = defState.childDefPosMPR;
						if ( rsRate < 0.001f ) {
							rotVec = getRotVecFromTgtBPos( tgtBPos, ref spring, cdpMPR );
							sftVec = Unity.Mathematics.float3.zero;
						} else if ( 0.999f < rsRate ) {
							rotVec = Unity.Mathematics.float3.zero;
							sftVec = spring.range_sft.local2global(tgtBPos - cdpMPR);
						} else {
							rotVec = getRotVecFromTgtBPos( tgtBPos, ref spring, cdpMPR ) * (1f - rsRate);
							sftVec = spring.range_sft.local2global((tgtBPos - cdpMPR) * rsRate);
						}
					}

					// バネ振動を更新
					if ( rsRate <= 0.999f ) {
						spring.spring_rot.x = rotVec;
						spring.spring_rot.update(dt);
						rotVec = spring.spring_rot.x;
					}
					if ( 0.001f <= rsRate ) {
						spring.spring_sft.x = sftVec;
						spring.spring_sft.update(dt);
						sftVec = spring.spring_sft.x;
					}

					// 現在の姿勢情報から、Transformに設定するための情報を構築
					quaternion rot;
					float3 trs;
					if ( rsRate <= 0.999f ) {
						var theta = length(rotVec);
						if ( theta < 0.001f ) {
							rot = defState.defRot;
						} else {
							var axis = rotVec / theta;
							var q = Unity.Mathematics.quaternion.AxisAngle(axis, theta);
							rot = mul(q, defState.defRot);
						}
					} else {
						rot = defState.defRot;
					}
					if ( 0.001f <= rsRate ) {
						trs = defState.defPos + sftVec;
					} else {
						trs = defState.defPos;
					}
					var result = new SpringResult{ rot=rot, trs=trs };


					// L2W行列をここで再計算する。
					// このL2WはTransformには直接反映されないので、2重計算になってしまうが、
					// 親から順番に処理を進めないといけないし、Transformへの値反映はここからは出来ないので
					// 仕方なくこうしている。
					float4x4 l2w;
					{
						var rotMtx = new float3x3(rot);
						var scl = defState.curScale;
						var l2p = float4x4(
							float4( rotMtx.c0*scl.x, 0 ),
							float4( rotMtx.c1*scl.y, 0 ),
							float4( rotMtx.c2*scl.z, 0 ),
							float4( trs, 1 )
						);
						l2w = mul(ppL2W, l2p);
					}

					// 現在のワールド位置を保存
					// これは正確なChildの現在位置ではなく、位置移動のみ考慮から外している。
					// 位置移動が入っている正確な現在位置で計算すると、位置Spring計算が正常に出来ないためである。
					wPosCache.lastWPos = mulMxPos(l2w, defState.childDefPos);

					// バッファを更新
					buf_spring[i] = spring;
					buf_result[i] = result;
					buf_wPosCache[i] = wPosCache;

					ppL2W = l2w;
				}
			}


			// コンポーネントへ値を反映
			for (int i=0; i<mostParent.depth; ++i) {
				var e = buf_entity[i];
				SetComponent(e, buf_spring[i]);
				SetComponent(e, buf_result[i]);
				SetComponent(e, buf_wPosCache[i]);
			}
			buf_spring.Dispose();
			buf_defState.Dispose();
			buf_wPosCache.Dispose();
			buf_result.Dispose();
			buf_entity.Dispose();

		}).Schedule(Dependency);



		// マネージド空間へ、結果を同期する
		// 本当はこれは並列にするまでもないが、
		// IJobParallelForTransformを使用しないとそもそもスレッド化できず、
		// そうなるとECSに乗せる事自体が瓦解するので、仕方なくこうしている
		var etp = _entityReg.etPacks;
		if (etp.Length != 0) {
			Dependency = new SpringTransUpdateJob{
				entities = etp.Entities,
				springResults = GetComponentDataFromEntity<SpringResult>(true),
			}.Schedule( etp.Transforms, Dependency );
		}

	}

	/** 同次変換行列に位置を掛けて変換後の位置を得る処理 */
	[MethodImpl(MethodImplOptions.AggressiveInlining)]
	static float3 mulMxPos(float4x4 mtx, float3 pos) => mul( mtx, float4(pos,1) ).xyz;

	// 先端目標位置へ移動した結果の姿勢ベクトルを得る処理
	static float3 getRotVecFromTgtBPos(
		float3 tgtBPos,
		ref OneSpring spring,
		float3 childDefPosMPR
	) {
		var childDefDir = normalize( childDefPosMPR );
		float3 ret;
		tgtBPos = normalize(tgtBPos);
		
		var crs = cross(childDefDir, tgtBPos);
		var crsNrm = length(crs);
		if ( crsNrm < 0.001f ) {
			ret = Unity.Mathematics.float3.zero;
		} else {
			var theta = acos( dot(childDefDir, tgtBPos) );
			theta = spring.range_rot.local2global( theta );
			ret = crs * (theta/crsNrm);
		}

		return ret;
	}

}
}
