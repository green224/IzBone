using System;
using UnityEngine.Jobs;
using Unity.Jobs;

using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using System.Runtime.CompilerServices;



namespace IzBone.IzBCollider.Core {
using Common;

[UpdateInGroup(typeof(IzBoneSystemGroup))]
[AlwaysUpdateSystem]
public sealed class IzBColliderSystem : SystemBase {

	// BodyAuthoringを登録・登録解除する処理
	internal void register(BodiesPackAuthoring auth, EntityRegisterer.RegLink regLink)
		=> _entityReg.register(auth, regLink);
	internal void unregister(BodiesPackAuthoring auth, EntityRegisterer.RegLink regLink)
		=> _entityReg.unregister(auth, regLink);
	internal void resetAllParameters() => _entityReg.resetAllParameters();
	EntityRegisterer _entityReg;

	/** マネージドTransformからECSへデータを反映させる処理 */
	[BurstCompile]
	struct MngTrans2ECSJob : IJobParallelForTransform
	{
		[ReadOnly] public NativeArray<Entity> entities;
		[ReadOnly] public ComponentDataFromEntity<Body_ShapeType> shapeTypes;
		[ReadOnly] public ComponentDataFromEntity<Body_Center> centers;
		[ReadOnly] public ComponentDataFromEntity<Body_Rot> rots;
		[ReadOnly] public ComponentDataFromEntity<Body_R> rs;

		[NativeDisableParallelForRestriction]
		[WriteOnly] public ComponentDataFromEntity<Body_RawCollider> rawColis;

		public void Execute(int index, TransformAccess transform)
		{
			var entity = entities[index];

			var shapeType = shapeTypes[entity].value;
			var center = centers[entity].value;
			var rot = rots[entity].value;
			var r = rs[entity].value;

			// L2Wを計算
			float4x4 l2w;
			if (shapeType == ShapeType.Sphere) {
				var tr = Unity.Mathematics.float4x4.identity;
				tr.c3.xyz = center;
				l2w = mul(transform.localToWorldMatrix, tr);
			} else {
				var tr = float4x4(rot, center);
				l2w = mul(transform.localToWorldMatrix, tr);
			}

			// RawColliderを生成
			var rawColi = new Body_RawCollider();
			switch (shapeType) {
			case ShapeType.Sphere :
				rawColi.sphere = new Collider_Sphere() {
					pos = l2w.c3.xyz,
					r = length( l2w.c0.xyz ) * r.x,
				};
				break;
			case ShapeType.Capsule : {
				var sclX = length( l2w.c0.xyz );
				var sclY = length( l2w.c1.xyz );
				rawColi.capsule = new Collider_Capsule() {
					pos = l2w.c3.xyz,
					r_s = sclX * r.x,
					r_h = sclY * r.y,
					dir = l2w.c1.xyz / sclY,
				};
				} break;
			case ShapeType.Box : {
				var sclX = length( l2w.c0.xyz );
				var sclY = length( l2w.c1.xyz );
				var sclZ = length( l2w.c2.xyz );
				rawColi.box = new Collider_Box() {
					pos = l2w.c3.xyz,
					xAxis = l2w.c0.xyz / sclX,
					yAxis = l2w.c1.xyz / sclY,
					zAxis = l2w.c2.xyz / sclZ,
					r = r * float3(sclX, sclY, sclZ),
				};
				} break;
			case ShapeType.Plane :
				rawColi.plane = new Collider_Plane() {
					pos = l2w.c3.xyz,
					dir = l2w.c2.xyz / length( l2w.c2.xyz ),
				};
				break;
			default : throw new InvalidProgramException();
			}

			// コンポーネントへデータを格納
			rawColis[entity] = rawColi;
		}
	}


	protected override void OnCreate() {
		_entityReg = new EntityRegisterer();
	}

	protected override void OnDestroy() {
		_entityReg.Dispose();
	}

	override protected void OnUpdate() {

		// 追加・削除されたAuthの情報をECSへ反映させる
		_entityReg.apply(EntityManager);

		// マネージドTransformから、ECSへL2Wを同期
		var etp = _entityReg.etPacks;
		if (etp.Length != 0) {
			Dependency = new MngTrans2ECSJob{
				entities = etp.Entities,
				shapeTypes = GetComponentDataFromEntity<Body_ShapeType>(true),
				centers = GetComponentDataFromEntity<Body_Center>(true),
				rots = GetComponentDataFromEntity<Body_Rot>(true),
				rs = GetComponentDataFromEntity<Body_R>(true),
				rawColis = GetComponentDataFromEntity<Body_RawCollider>(false),
			}.Schedule( etp.Transforms, Dependency );
		}
	}

}
}
