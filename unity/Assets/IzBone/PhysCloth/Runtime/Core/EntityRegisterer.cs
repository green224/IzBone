
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;


namespace IzBone.PhysCloth.Core {
	using Common.Entities8;

	/** BodyAuthのデータをもとに、Entityを生成するモジュール */
	public sealed class EntityRegisterer : EntityRegistererBase<Authoring.BaseAuthoring>
	{
		// ------------------------------------- public メンバ ----------------------------------------

		public EntityRegisterer() : base(128) {}


		// --------------------------------- private / protected メンバ -------------------------------

		const float MinimumM = 0.00000001f;

		/** Auth1つ分の変換処理 */
		override protected void convertOne(
			Authoring.BaseAuthoring auth,
			RegLink regLink,
			EntityManager em
		) {

			// ParticleのEntity一覧をまず作成
			var ptclEntities = new NativeArray<Entity>();
			for (int i=0; i<auth._particles.Length; ++i) {
				var entity = em.CreateEntity();
				ptclEntities[i] = entity;
			}

			// ParticleのEntityの中身を作成
			for (int i=0; i<auth._particles.Length; ++i) {
				var mp = auth._particles[i];
				var entity = ptclEntities[i];

				// コンポーネントを割り当て
				em.AddComponentData(entity, new Ptcl());
				var nextEnt = i==ptclEntities.Length-1 ? default : ptclEntities[i+1];
				em.AddComponentData(entity, new Ptcl_Next{value = nextEnt});
				var parentEnt = mp.parent==null ? default : ptclEntities[mp.parent.idx];
				em.AddComponentData(entity, new Ptcl_Parent{value = parentEnt});
				em.AddComponentData(entity, new Ptcl_DefaultL2W());
				em.AddComponentData(entity, new Ptcl_DefaultL2P{value = mp.defaultL2P});
				em.AddComponentData(entity, new Ptcl_DefaultParentRot{value = mp.defaultParentRot});
				var sphere = new IzBCollider.RawCollider.Sphere{ pos=mp.trans.position, r=mp.r };
				em.AddComponentData(entity, new Ptcl_Sphere{value = sphere});
				em.AddComponentData(entity, new Ptcl_V());
				em.AddComponentData(entity, new Ptcl_InvM(mp.m));
				em.AddComponentData(entity, new Ptcl_DWRot());
				em.AddComponentData(entity, new Ptcl_MaxAngle{value = mp.maxAngle});
				em.AddComponentData(entity, new Ptcl_AngleCompliance{value = mp.angleCompliance});
				em.AddComponentData(entity, new Ptcl_RestoreHL{value = mp.restoreHL});
				em.AddComponentData(entity, new Ptcl_MaxMovableRange{value = mp.maxMovableRange});
				em.AddComponentData(entity, new Ptcl_M2D{auth = mp});

				// Entity・Transformを登録
				addEntityCore(entity, regLink);
				addETPCore(entity, mp.trans, regLink);
			}

			// ConstraintのEntityを作成
			for (int i=0; i<auth._constraints.Length; ++i) {
				var mc = auth._constraints[i];

				// 強度があまりにも弱い場合は拘束条件を追加しない
				if (Authoring.ComplianceAttribute.LEFT_VAL*0.98f < mc.compliance) continue;

				Entity entity = default;
				switch (mc.mode) {
				case Authoring.ConstraintMng.Mode.Distance:
					{// 距離拘束

						// 対象パーティクルがどちらも固定されている場合は無効
						var srcMP = auth._particles[mc.srcPtclIdx];
						var dstMP = auth._particles[mc.dstPtclIdx];
						if (srcMP.m < Ptcl_InvM.MinimumM && dstMP.m < Ptcl_InvM.MinimumM) break;

						// コンポーネントを割り当て
						entity = em.CreateEntity();
						em.AddComponentData(entity, new DistCstr());
						var srcEnt = ptclEntities[mc.srcPtclIdx];
						var dstEnt = ptclEntities[mc.dstPtclIdx];
						em.AddComponentData(entity, new Cstr_Target{src=srcEnt, dst=dstEnt});
						em.AddComponentData(entity, new Cstr_Compliance{value = mc.compliance});
						em.AddComponentData(entity, new Cstr_DefaultLen{value = mc.param.x});

					} break;
				case Authoring.ConstraintMng.Mode.MaxDistance:
					{// 最大距離拘束
// 未対応
//						var b = new MaxDistance{
//							compliance = i.compliance,
//							src = i.param.xyz,
//							tgt = pntsPtr + i.srcPtclIdx,
//							maxLen = i.param.w,
//						};
//						if ( b.isValid() ) md.Add( b );
					} break;
				case Authoring.ConstraintMng.Mode.Axis:
					{// 稼働軸拘束
// 未対応
//						var b = new Axis{
//							compliance = i.compliance,
//							src = pntsPtr + i.srcPtclIdx,
//							dst = pntsPtr + i.dstPtclIdx,
//							axis = i.param.xyz,
//						};
//						if ( b.isValid() ) a.Add( b );
					} break;
				default:throw new System.InvalidProgramException();
				}

				// Entity・Transformを登録
				if (entity != default) addEntityCore(entity, regLink);
			}



			{// OneClothをECSへ変換
				em.AddComponentData(ptclEntities[0], new OneCloth());
				auth._rootEntity = ptclEntities[0];
			}


			ptclEntities.Dispose();
		}

		/** 指定Entityの再変換処理 */
		override protected void reconvertOne(Entity entity, EntityManager em) {
		}
		

		// --------------------------------------------------------------------------------------------
	}
}