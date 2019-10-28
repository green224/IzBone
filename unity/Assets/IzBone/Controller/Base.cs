using System;
using UnityEngine;



namespace IzBone.Controller {

	/** IzBoneを使用するオブジェクトにつけるコンポーネントの基底クラス */
	public unsafe abstract class Base : MonoBehaviour {
		// ------------------------------- inspectorに公開しているフィールド ------------------------

		[SerializeField] protected IzCollider[] _izColliders = null;


		// ----------------------------------- private/protected メンバ -------------------------------

		protected Core.Colliders _coreColliders = new Core.Colliders();

		virtual protected void Start() {
			if (!Application.isPlaying) return;
			_coreColliders.setup();
		}

		virtual protected void OnDestroy() {
			if (!Application.isPlaying) return;
			_coreColliders.release();
		}

		virtual protected void coreUpdate(float dt) {
			foreach (var i in _izColliders) i.update_phase0();
			foreach (var i in _izColliders) i.update_phase1();
			_coreColliders.update(_izColliders);
		}

	#if UNITY_EDITOR
		/** 配下のIzColliderを全登録する */
		[ContextMenu("Collect child IzColliders")]
		void collectChildIzCol() {
			_izColliders = GetComponentsInChildren< IzCollider >();
		}

		virtual protected void OnDrawGizmos() {
			if ( !UnityEditor.Selection.Contains( gameObject.GetInstanceID() ) ) return;

			// 登録されているコライダを表示
			if (_izColliders!=null) foreach (var i in _izColliders) i.DEBUG_drawGizmos();
		}
	#endif

		// --------------------------------------------------------------------------------------------
	}

}

