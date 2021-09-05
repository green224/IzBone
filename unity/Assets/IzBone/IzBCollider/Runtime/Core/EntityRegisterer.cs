
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

		/** Auth1つ分の変換処理 */
		override protected void convertOne(
			BodiesPackAuthoring auth,
			RegLink regLink,
			EntityManager em
		) {

			// 参照先のBodyをECSへ変換
			Entity firstBody = default;
			{
				Entity lastBody = default;
				foreach (var i in auth.Bodies) {

					// コンポーネントを割り当て
					var entity = em.CreateEntity();
					if (firstBody == default) firstBody = entity;
					if (lastBody != default)
						em.AddComponentData(lastBody, new Body_Next{value=entity});
					em.AddComponentData(entity, new Body());
					em.AddComponentData(entity, new Body_ShapeType{value=i.mode});
					em.AddComponentData(entity, new Body_Center{value=i.center});
					em.AddComponentData(entity, new Body_R{value=i.r});
					em.AddComponentData(entity, new Body_Rot{value=i.rot});
					em.AddComponentData(entity, new Body_RawCollider());
					em.AddComponentData(entity, new Body_M2D{bodyAuth=i});

					// Entity・Transformを登録
					addEntityCore(entity, regLink);
					addETPCore(entity, i.transform, regLink);

					lastBody = entity;
				}
				if (lastBody != default)
					em.AddComponentData(lastBody, new Body_Next());
			}


			{// BodiesPackをECSへ変換
				var entity = em.CreateEntity();
				em.AddComponentData(entity, new BodiesPack{first=firstBody});
				addEntityCore(entity, regLink);
				auth._rootEntity = entity;
			}
		}

		/** 登録済みの全Authの再変換処理 */
		override protected void reconvertAll(EntityManager em) {
			foreach (var i in _entities) {
				if (!em.HasComponent<Body_M2D>(i.e)) continue;

				var auth = em.GetComponentData<Body_M2D>(i.e).bodyAuth;
				em.SetComponentData(i.e, new Body_ShapeType{value=auth.mode});
				em.SetComponentData(i.e, new Body_Center{value=auth.center});
				em.SetComponentData(i.e, new Body_R{value=auth.r});
				em.SetComponentData(i.e, new Body_Rot{value=auth.rot});
			}
		}
		

		// --------------------------------------------------------------------------------------------
	}
}