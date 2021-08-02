using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

using System.Collections.Generic;


namespace IzPhysBone.Cloth.Controller {

	/**
	 * IzBoneを使用するオブジェクトにつけるコンポーネント。
	 * 平面的な布のようなものを再現する際に使用する
	 */
	[ExecuteInEditMode]
	[AddComponentMenu("IzBone/Rope")]
	public unsafe sealed class Rope : Base {
		// ------------------------------- inspectorに公開しているフィールド ------------------------

		/** 骨の情報 */
		[Serializable] sealed class BoneInfo {
			public Transform boneTop = null;
			public int depth = 1;
			[SerializeField] AnimationCurve _m = null;
			[SerializeField] AnimationCurve _r = null;

			[NonSerialized] public Point point = null;

			public float getM(int idx) => Math.Max(0, _m.Evaluate( depth==1 ? 0 : ((float)idx/(depth-1)) ) );
			public float getR(int idx) => Math.Max(0, _r.Evaluate( depth==1 ? 0 : ((float)idx/(depth-1)) ) );
		}
		[SerializeField] BoneInfo _boneInfo = null;

		[Space]
		[Compliance][SerializeField] float _cmpl_direct = 0.000000001f;		//!< Compliance値 直接接続
		[Compliance][SerializeField] float _cmpl_bend = 0.00002f;			//!< Compliance値 曲げ用の１つ飛ばし接続
		[Compliance][SerializeField] float _cmpl_roll = 0.0000001f;			//!< Compliance値 捻じれ用の対角線接続


		// --------------------------------------- publicメンバ -------------------------------------

		// ----------------------------------- private/protected メンバ -------------------------------

		override protected void Start() {
			base.Start();
			if (!Application.isPlaying) return;

			// 質点リストを構築
			var points = new List<Point>();
			{
				Point p = _boneInfo.point = new Point(points.Count, _boneInfo.boneTop) {
					m = _boneInfo.getM(0),
					r = _boneInfo.getR(0),
				};
				points.Add(p);

				int k = 1;
				for (var j=p.trans; k<_boneInfo.depth; ++k) {
					j=j.GetChild(0);
					var newP = new Point(points.Count, j) {
						parent = p,
						m = _boneInfo.getM(k),
						r = _boneInfo.getR(k),
					};
					p.child = newP;
					p = newP;
					points.Add(p);
				}
			}
			_points = points.ToArray();

			// 制約リストを構築
			_constraints.Clear();
			{
				var c = _boneInfo.point;
				var d0 = c.child;
				var d1 = d0?.child;

				int depth=0;
				while (c!=null) {
					if (d0!=null) addCstr(_cmpl_direct, c, d0);
					if (d1!=null) addCstr(_cmpl_bend,   c, d1);

					c=c.child;
					d0=d0?.child;
					d1=d1?.child;
					++depth;
				}
			}

			begin();
		}

	#if UNITY_EDITOR
		void Update() {
			if (Application.isPlaying) return;

			if (_boneInfo == null) return;
			{
				// Depthを有効範囲に丸める
				if ( _boneInfo.boneTop == null ) {
					_boneInfo.depth = 0;
				} else {
					int depthMax = 1;
					for (var j=_boneInfo.boneTop; j.childCount!=0; j=j.GetChild(0)) ++depthMax;

					_boneInfo.depth = Mathf.Clamp(_boneInfo.depth, 1, depthMax);
				}
			}
		}
	#endif
			
		void LateUpdate() {
			if (!Application.isPlaying) return;
			coreUpdate(Time.deltaTime);
		}

	#if UNITY_EDITOR
		override protected void OnDrawGizmos() {
			base.OnDrawGizmos();
			if ( !UnityEditor.Selection.Contains( gameObject.GetInstanceID() ) ) return;

			if (_boneInfo == null) return;
			if (_boneInfo.boneTop != null) {

				var trans = _boneInfo.boneTop;
				for (int dCnt=0; dCnt!=_boneInfo.depth; ++dCnt) {
					Gizmos.color = new Color(1,1,1,0.5f);
					Gizmos.DrawSphere( trans.position, _boneInfo.getR(dCnt) );

					if (!Application.isPlaying) {
						Gizmos.color = new Color(1,1,0);
						if (dCnt != 0) Gizmos.DrawLine( trans.position, trans.parent.position );
					}

					if ( trans.childCount==0 ) break; else trans=trans.GetChild(0);
				}
			}
		}
	#endif

		// --------------------------------------------------------------------------------------------
	}

}

