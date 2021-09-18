
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.IzBCollider.Core {
	using Common.Entities8;

	/** BodyAuthのデータをもとに、Entityを生成するモジュール */
	public sealed class EntityRegisterer : EntityRegistererBase<BodiesPackAuthoring>
	{
		// ------------------------------------- public メンバ ----------------------------------------

		public EntityRegisterer() : base(128) {}


		// --------------------------------- private / protected メンバ -------------------------------

		static readonly System.Action<EntityManager,Entity>[] _genRawBody = new System.Action<EntityManager,Entity>[] {
			(em,entity) => em.AddComponentData(entity, new Body_Raw_Sphere()),
			(em,entity) => em.AddComponentData(entity, new Body_Raw_Capsule()),
			(em,entity) => em.AddComponentData(entity, new Body_Raw_Box()),
			(em,entity) => em.AddComponentData(entity, new Body_Raw_Plane()),
		};

		/** Auth1つ分の変換処理 */
		override protected void convertOne(
			BodiesPackAuthoring auth,
			RegLink regLink,
			EntityManager em
		) {

			var firstBody = new Entity[(int)ShapeType.MAX_COUNT];
			var lastBody = new Entity[(int)ShapeType.MAX_COUNT];
			for (int i=0; i<firstBody.Length; ++i) {
				firstBody[i] = Entity.Null;
				lastBody[i] = Entity.Null;
			}

			// 参照先のBodyをECSへ変換
			{
				foreach (var i in auth.Bodies) {

					int shapeId = (int)i.mode;

					// コンポーネントを割り当て
					var entity = em.CreateEntity();

					if (firstBody[shapeId] == Entity.Null) firstBody[shapeId] = entity;
					if (lastBody[shapeId] != Entity.Null)
						em.AddComponentData(lastBody[shapeId], new Body_Next{value=entity});
					em.AddComponentData(entity, new Body());
					em.AddComponentData(entity, new Body_Center{value=i.center});
					em.AddComponentData(entity, new Body_R{value=i.r});
					var rot = i.mode == ShapeType.Sphere ? default : i.rot;
					em.AddComponentData(entity, new Body_Rot{value=rot});
					_genRawBody[shapeId](em, entity);
					em.AddComponentData(entity, new Body_CurL2W());
					em.AddComponentData(entity, new Body_M2D{bodyAuth=i});

					// Entity・Transformを登録
					addEntityCore(entity, regLink);
					addETPCore(entity, i.transform, regLink);

					lastBody[shapeId] = entity;
				}
				foreach (var i in lastBody)
					if (i != Entity.Null) em.AddComponentData(i, new Body_Next());
			}


			{// BodiesPackをECSへ変換
				var entity = em.CreateEntity();
				em.AddComponentData(entity, new BodiesPack{
					firstSphere  = firstBody[0],
					firstCapsule = firstBody[1],
					firstBox     = firstBody[2],
					firstPlane   = firstBody[3],
				});
				auth.setRootEntity( entity, em );
			}
		}

		/** 指定Entityの再変換処理 */
		override protected void reconvertOne(Entity entity, EntityManager em) {
			if (!em.HasComponent<Body_M2D>(entity)) return;

			var auth = em.GetComponentData<Body_M2D>(entity).bodyAuth;
			em.SetComponentData(entity, new Body_Center{value=auth.center});
			em.SetComponentData(entity, new Body_R{value=auth.r});
			em.SetComponentData(entity, new Body_Rot{value=auth.rot});
		}
		

		// --------------------------------------------------------------------------------------------
	}
}