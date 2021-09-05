using System;
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.IzBCollider {

	/** IzBone専用のコライダーの対象コライダーを一つにパックするためのコンポーネント */
	[AddComponentMenu("IzBone/IzBone_CollidersPack")]
	public sealed class BodiesPackAuthoring : MonoBehaviour {
		// ------------------------------- inspectorに公開しているフィールド ------------------------

		[SerializeField] BodyAuthoring[] _bodies = new BodyAuthoring[0];


		// --------------------------------------- publicメンバ -------------------------------------

		public BodyAuthoring[] Bodies => _bodies;

		// このBodiesPackの対応するルートのEntity
		public Entity RootEntity => _rootEntity;


		// ----------------------------------- private/protected メンバ -------------------------------

		/** ECSで得た結果をマネージドTransformに反映するためのバッファのリンク情報。System側から設定・参照される */
		Core.EntityRegisterer.RegLink _erRegLink = new Core.EntityRegisterer.RegLink();

		// このBodiesPackの対応するルートのEntity。EntityRegistererから設定される
		internal Entity _rootEntity;

		/** メインのシステムを取得する */
		Core.IzBColliderSystem GetSys() {
			var w = World.DefaultGameObjectInjectionWorld;
			if (w == null) return null;
			return w.GetOrCreateSystem<Core.IzBColliderSystem>();
		}

		void OnEnable()
		{
			var sys = GetSys();
			if (sys != null) sys.register(this, _erRegLink);
		}

		void OnDisable()
		{
			var sys = GetSys();
			if (sys != null) sys.unregister(this, _erRegLink);
			_rootEntity = default;
		}


		// --------------------------------------------------------------------------------------------
	}

}

