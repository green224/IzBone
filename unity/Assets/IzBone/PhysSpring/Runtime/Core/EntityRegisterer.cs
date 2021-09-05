
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysSpring.Core {
	using Common.Entities8;
	using Common.Field;

	/** RootAuthのデータをもとに、Entityを生成するモジュール */
	public sealed class EntityRegisterer : EntityRegistererBase<RootAuthoring>
	{
		// ------------------------------------- public メンバ ----------------------------------------

		public EntityRegisterer() : base(128) {}


		// --------------------------------- private / protected メンバ -------------------------------

		/** Auth1つ分の変換処理 */
		override protected void convertOne(
			RootAuthoring auth,
			RegLink regLink,
			EntityManager em
		) {

			foreach (var bone in auth._bones)
			foreach (var j in bone.targets) {
				var child = j.endOfBone;
				Entity childEntity = default;

				// OneSpringコンポーネントを生成
				for (int i=0; i<bone.depth; ++i) {
					var parent = child.parent;

					// 無効なDepth値が指定されていた場合はエラーを出す
					if (parent == null) {
						UnityEngine.Debug.LogError("PhySpring:depth is too higher");
						continue;
					}

//					var dRate = (float)(bone.depth-1-i) / max(bone.depth-1, 1);
					var dRate = remap(0, bone.depth-1, 1, 0, i);

					// デフォルト姿勢を適応
					if (i == bone.depth-1) {
						if (!j.defaultRot.Equals(default))		// defaultRotが未初期化の場合があるので、そうでないかチェックする
							parent.localRotation *= j.defaultRot;
					}

					// コンポーネントを割り当て
					var entity = em.CreateEntity();
					em.AddComponentData(entity, genOneSpring(bone, dRate));
					em.AddComponentData(entity, new DefaultState{
						resetDefPosAlways = bone.withAnimation,
						defRot = parent.localRotation,
						defPos = parent.localPosition,
						childDefPos = child.localPosition,
						childDefPosMPR = mul(
							parent.localRotation,
							parent.localScale * (float3)child.localPosition
						),
					});
					em.AddComponentData(entity, new SpringResult{});
					em.AddComponentData(entity, new WPosCache{
						lastWPos = child.position,
					});
					em.AddComponentData(entity, new OneSpring_M2D{
						boneAuth = bone,
						depthRate = dRate,
						parentTrans = parent,
						childTrans = child,
					});
					if (i!=0)
						em.AddComponentData(entity, new Child{value = childEntity});
					if (i==bone.depth-1) {
						Entity colliderPackEntity = default;
						if (auth._collider != null)
							colliderPackEntity = auth._collider.RootEntity;
						em.AddComponentData(entity, new MostParent{
							depth = bone.depth,
							iterationNum = bone.iterationNum,
							rsRate = bone.rotShiftRate,
							colliderPack = colliderPackEntity,
						});
					}

					// OneSpringのEntity・Transformを登録
					addEntityCore(entity, regLink);
					addETPCore(entity, parent, regLink);

					child = parent;
					childEntity = entity;
				}
			}
		}

		/** 登録済みの全Authの再変換処理 */
		override protected void reconvertAll(EntityManager em) {
			// 出来るものだけ同期を行う
			foreach (var i in _entities) {
				var m2d = em.GetComponentData<OneSpring_M2D>(i.e);
				em.SetComponentData(i.e, genOneSpring(m2d.boneAuth, m2d.depthRate));

				if (em.HasComponent<MostParent>(i.e)) {
					var mp = em.GetComponentData<MostParent>(i.e);
					mp.iterationNum = m2d.boneAuth.iterationNum;
					mp.rsRate = m2d.boneAuth.rotShiftRate;
					em.SetComponentData(i.e, mp);
				}
			}
		}

		static OneSpring genOneSpring(RootAuthoring.Bone bone, float dRate) {
			var ret = new OneSpring{};
			var rotMax = radians( bone.angleMax.evaluate(dRate) );
			var rotMargin = rotMax * bone.angleMargin.evaluate(dRate);
			ret.range_rot.reset(-rotMax, rotMax, rotMargin);
			var shiftMax = bone.shiftMax.evaluate(dRate);
			var shiftMargin = shiftMax * bone.shiftMargin.evaluate(dRate);
			ret.range_sft.reset(-shiftMax, shiftMax, shiftMargin);

			// バネを初期化
			ret.spring_rot.kpm = bone.rotKpm.evaluate(dRate);
			ret.spring_rot.maxV = bone.omgMax.evaluate(dRate);
			ret.spring_rot.maxX = ret.range_rot.localMax;
			ret.spring_rot.vHL = HalfLifeDragAttribute.showValue2HalfLife(
				bone.omgDrag.evaluate(dRate)
			);
			ret.spring_sft.kpm = bone.shiftKpm.evaluate(dRate);
			ret.spring_sft.maxV = bone.vMax.evaluate(dRate);
			ret.spring_sft.maxX = ret.range_sft.localMax.x;
			ret.spring_sft.vHL = HalfLifeDragAttribute.showValue2HalfLife(
				bone.vDrag.evaluate(dRate)
			);

			return ret;
		}


		// --------------------------------------------------------------------------------------------
	}
}