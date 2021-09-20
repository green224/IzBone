using System;
using UnityEngine;

using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysCloth.Authoring {
using Common;
using Common.Field;

/** IzBoneを使用するオブジェクトにつけるコンポーネントの基底クラス */
public unsafe abstract class BaseAuthoring
: PhysBone.Body.BaseAuthoringT<BaseAuthoring, Core.IzBPhysClothSystem>
{
	// ------------------------------- inspectorに公開しているフィールド ------------------------

	// --------------------------------------- publicメンバ -------------------------------------

	[Space]
	public bool useSimulation = true;				// 物理演算を行うか否か
	[Range(1,50)] public int iterationNum = 15;		// 1frame当たりの計算イテレーション回数

	[Space]
	public Gravity g = new Gravity(1);				// 重力加速度
	public float3 windSpeed = default;				// 風速
	[HalfLifeDrag] public HalfLife airDrag = 0.1f;	// 空気抵抗による半減期
	[Min(0)] public float maxSpeed = 100;			// 最大速度

	[Space]
	// アニメーション付きのボーンに対して使用するフラグ。毎フレームデフォルト位置を再キャッシュする。
	// リグを付けたスカートなどの場合もこれをONにして使用する。
	public bool withAnimation = false;


	// ----------------------------------- private/protected メンバ -------------------------------

	internal ConstraintMng[] _constraints;
	internal ParticleMng[] _particles;
	internal Entity _rootEntity = Entity.Null;


	override protected void OnEnable() {
		buildBuffers();
		rebuildParameters();

		base.OnEnable();
	}


	/** ParticlesとConstraintsのバッファをビルドする処理。派生先で実装すること */
	abstract protected void buildBuffers();

	/** ParticlesとConstraintsのパラメータを再構築する処理。派生先で実装すること */
	abstract protected void rebuildParameters();


	/** パラメータをリビルドして、システムへパラメータ同期を通知する */
	override protected void rebuildAndResetParam() {
		rebuildParameters();
		base.rebuildAndResetParam();
	}


	// --------------------------------------------------------------------------------------------
}

}

