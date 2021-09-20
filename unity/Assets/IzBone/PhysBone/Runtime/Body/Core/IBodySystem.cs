//#define WITH_DEBUG
using System;
using UnityEngine.Jobs;
using Unity.Jobs;

using Unity.Burst;
using Unity.Entities;
using Unity.Collections;
using Unity.Mathematics;
using static Unity.Mathematics.math;



namespace IzBone.PhysBone.Body.Core {


	/** Bodyが実装すべきSystemのInterface */
	public interface IBodySystem<Authoring>
	where Authoring : BaseAuthoring
	{

		// Authoringを登録・登録解除する処理
		void register(
			Authoring auth,
			Common.Entities8.EntityRegistererBase<Authoring>.RegLink regLink
		);
		void unregister(
			Authoring auth,
			Common.Entities8.EntityRegistererBase<Authoring>.RegLink regLink
		);
		void resetParameters(
			Common.Entities8.EntityRegistererBase<Authoring>.RegLink regLink
		);

		/** 指定のAuthの物理状態をリセットする */
		void reset(Common.Entities8.EntityRegistererBase<Authoring>.RegLink regLink);


	}


}
