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
		[WriteOnly] public ComponentDataFromEntity<Body_L2W> l2ws;
		[WriteOnly] public ComponentDataFromEntity<Body_L2wClmNorm> l2wClmNorms;

		public void Execute(int index, TransformAccess transform)
		{
			var entity = entities[index];

			var shapeType = shapeTypes[entity].value;
			var center = centers[entity].value;
			var rot = rots[entity].value;

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

			// コンポーネントへデータを格納
			l2ws[entity] = new Body_L2W{value=l2w};
			l2wClmNorms[entity] = new Body_L2wClmNorm{
				value=float3(
					length( l2w.c0.xyz ),
					length( l2w.c1.xyz ),
					length( l2w.c2.xyz )
				)
			};
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
				l2ws = GetComponentDataFromEntity<Body_L2W>(false),
				l2wClmNorms = GetComponentDataFromEntity<Body_L2wClmNorm>(false),
			}.Schedule( etp.Transforms, Dependency );
		}

	}

}
}
