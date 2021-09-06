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

#if UNITY_EDITOR
			foreach (var i in _bodies) i.__parents.Add(this);
#endif
		}

		void OnDisable()
		{
			var sys = GetSys();
			if (sys != null) sys.unregister(this, _erRegLink);
			_rootEntity = default;

#if UNITY_EDITOR
			foreach (var i in _bodies) i.__parents.Remove(this);
#endif
		}


		// --------------------------------------------------------------------------------------------
#if UNITY_EDITOR
		// BodyのOnValidate時に、Bodyから呼ばれるコールバック
		internal void __onValidateBody() {
			if (Application.isPlaying) {
				var sys = GetSys();
				if (sys != null) sys.resetParameters(_erRegLink);
			}
		}
#endif
	}

}

