
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysSpring.Core {
	using Common.Entities8;

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
					var iRate = (float)(bone.depth-1-i) / max(bone.depth-1, 1);

					// デフォルト姿勢を適応
					if (i == bone.depth-1) {
						if (!j.defaultRot.Equals(default))		// defaultRotが未初期化の場合があるので、そうでないかチェックする
							parent.localRotation *= j.defaultRot;
					}


					// 範囲情報を初期化
					var a = new OneSpring{};
					var rotMax = radians( bone.angleMax.evaluate(iRate) );
					var rotMargin = rotMax * bone.angleMargin.evaluate(iRate);
					a.range_rot.reset(-rotMax, rotMax, rotMargin);
					var shiftMax = bone.shiftMax.evaluate(iRate);
					var shiftMargin = shiftMax * bone.shiftMargin.evaluate(iRate);
					a.range_sft.reset(-shiftMax, shiftMax, shiftMargin);

					// バネを初期化
					a.spring_rot.kpm = bone.rotKpm.evaluate(iRate);
					a.spring_rot.maxV = bone.omgMax.evaluate(iRate);
					a.spring_rot.maxX = a.range_rot.localMax;
					a.spring_rot.vHL = bone.omgHL.evaluate(iRate);
					a.spring_sft.kpm = bone.shiftKpm.evaluate(iRate);
					a.spring_sft.maxV = bone.vMax.evaluate(iRate);
					a.spring_sft.maxX = a.range_sft.localMax.x;
					a.spring_sft.vHL = bone.vHL.evaluate(iRate);
						
					// コンポーネントを割り当て
					var entity = em.CreateEntity();
					em.AddComponentData(entity,a);
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
						parentTrans = parent,
						childTrans = child,
					});
					if (i!=0)
						em.AddComponentData(entity, new Child{value = childEntity});
					if (i==bone.depth-1) {
						em.AddComponentData(entity, new MostParent{
							depth = bone.depth,
							iterationNum = bone.iterationNum,
							rsRate = bone.rotShiftRate,
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


		// --------------------------------------------------------------------------------------------
	}
}