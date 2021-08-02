using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

using System.Collections.Generic;


namespace IzBone.BonePhysics.Cloth.Controller {

	/**
	 * IzBoneを使用するオブジェクトにつけるコンポーネント。
	 * 平面的な布のようなものを再現する際に使用する
	 */
	[ExecuteInEditMode]
	[AddComponentMenu("IzBone/BonePhysics_Cloth_Plain")]
	public unsafe sealed class Plain : Base {
		// ------------------------------- inspectorに公開しているフィールド ------------------------

		/** ボーンをランタイム時バッファへの変換するときの情報 */
		[Serializable] sealed class ConversionParam {
			public int depth = 1;
			public int excludeSideChain = 0;
			public int sideChainDepth => depth - excludeSideChain;
			[SerializeField] AnimationCurve _m = new AnimationCurve(
				new Keyframe(0, 1),
				new Keyframe(1, 1)
			);
			[SerializeField] AnimationCurve _r = new AnimationCurve(
				new Keyframe(0, 1),
				new Keyframe(1, 1)
			);
			[SerializeField] AnimationCurve _maxAngle = new AnimationCurve(
				new Keyframe(0, 90),
				new Keyframe(1, 90)
			);

			public float getM(int idx) => max(0, _m.Evaluate( idx2rate(idx) ) );
			public float getR(int idx) => max(0, _r.Evaluate( idx2rate(idx) ) );
			public float getMaxAgl(int idx) => max(0, _maxAngle.Evaluate( idx2rate(idx) ) );

			float idx2rate(int idx) => depth==1 ? 0 : ((float)idx/(depth-1));
		}

		/** 骨の情報 */
		[Serializable] sealed class BoneInfo {
			public Transform boneTop = null;

			// ローカルConversionParamを使用するか否か
			public bool useLocalCnvPrm = false;
			// ローカルConversionParam
			public ConversionParam cnvPrm = new ConversionParam();

			[NonSerialized] public Point point = null;
		}
		[SerializeField] BoneInfo[] _boneInfos = null;

		[Space]
		// グローバルConversionParam
		[SerializeField] ConversionParam _cnvPrm = new ConversionParam();
		[SerializeField] bool _isLoopConnect = false;		// スカートなどの筒状のつながりにする

		[Space]
		[Compliance][SerializeField] float _cmpl_direct = 0.000000001f;		//!< Compliance値 直接接続
		[Compliance][SerializeField] float _cmpl_side = 0.000000001f;		//!< Compliance値 横方向の接続
		[Compliance][SerializeField] float _cmpl_diag = 0.0000001f;			//!< Compliance値 捻じれ用の対角線接続
		[Compliance][SerializeField] float _cmpl_bend = 0.00002f;			//!< Compliance値 曲げ用の１つ飛ばし接続


		// --------------------------------------- publicメンバ -------------------------------------

		// ----------------------------------- private/protected メンバ -------------------------------

		override protected void Start() {
			base.Start();
			if (!Application.isPlaying) return;

			// 質点リストを構築
			var points = new List<Point>();
			foreach ( var i in _boneInfos ) {
				var cnvPrm = getCnvPrm(i);
				Point p = i.point = new Point(points.Count, i.boneTop) {
					m = cnvPrm.getM(0),
					r = cnvPrm.getR(0),
					maxAngle = cnvPrm.getMaxAgl(0),
				};
				points.Add(p);
				if (cnvPrm.depth <= 1) continue;

				int k = 1;
				for (var j=p.trans; k<cnvPrm.depth; ++k) {
					j=j.GetChild(0);
					var newP = new Point(points.Count, j) {
						parent = p,
						m = cnvPrm.getM(k),
						r = cnvPrm.getR(k),
						maxAngle = cnvPrm.getMaxAgl(k),
					};
					p.child = newP;
					p = newP;
					points.Add(p);
				}
			}
			_points = points.ToArray();

			// 制約リストを構築
			_constraints.Clear();
			for (int i=0; i<_boneInfos.Length; ++i) {
				var (bl1, bl0, bc, br0, br1) = getSideBoneInfos(i);
				var c = bc.point;
				var l0 = br0?.point; var l1 = br1?.point;
				var r0 = bl0?.point; var r1 = bl1?.point;
				var d0 = c.child;    var d1 = d0?.child;
				var ld0 = l0?.child; var ld1 = l1?.child?.child;
				var rd0 = r0?.child; var rd1 = r1?.child?.child;

				int depth=0;
				while (c!=null) {
					if (d0 !=null && isChain(4,i,depth)) addCstr(_cmpl_side,   c, d0);
					if (d1 !=null && isChain(5,i,depth)) addCstr(_cmpl_bend,   c, d1);
					if (l0 !=null && isChain(0,i,depth)) addCstr(_cmpl_direct, c, l0);
					if (l1 !=null && isChain(1,i,depth)) addCstr(_cmpl_bend,   c, l1);
					if (ld0!=null && isChain(2,i,depth)) addCstr(_cmpl_diag,   c, ld0);
					if (ld1!=null && isChain(3,i,depth)) addCstr(_cmpl_bend,   c, ld1);
					if (rd0!=null && isChain(6,i,depth)) addCstr(_cmpl_diag,   c, rd0);
					if (rd1!=null && isChain(7,i,depth)) addCstr(_cmpl_bend,   c, rd1);

					c=c.child;
					l0=l0?.child;
					l1=l1?.child;
					d0=d0?.child;
					d1=d1?.child;
					ld0=ld0?.child;
					ld1=ld1?.child;
					rd0=rd0?.child;
					rd1=rd1?.child;
					++depth;
				}
			}

			begin();
		}

		void LateUpdate() {
			if (!Application.isPlaying) return;
			coreUpdate(Time.deltaTime);
		}

		/** BoneInfoから、グローバルConversionParamを使用するか否かを考慮して、ConversionParamを取得する */
		ConversionParam getCnvPrm(BoneInfo boneInfo) =>
			boneInfo.useLocalCnvPrm ? boneInfo.cnvPrm : _cnvPrm;

		/** 指定のインデックスのBoneInfoを左右のものもセットで取得する */
		(
			BoneInfo bl1, BoneInfo bl0,
			BoneInfo bc,
			BoneInfo br0, BoneInfo br1
		) getSideBoneInfos(int idx) {
			int idxR0 = (idx+1) % _boneInfos.Length;
			int idxR1 = (idx+2) % _boneInfos.Length;
			int idxL0 = (idx-1+_boneInfos.Length) % _boneInfos.Length;
			int idxL1 = (idx-2+_boneInfos.Length) % _boneInfos.Length;

			return (
				(0<=idx-2||_isLoopConnect ? _boneInfos[idxL1] : null),
				(0<=idx-1||_isLoopConnect ? _boneInfos[idxL0] : null),
				_boneInfos[idx],
				(idx+1<_boneInfos.Length||_isLoopConnect ? _boneInfos[idxR0] : null),
				(idx+2<_boneInfos.Length||_isLoopConnect ? _boneInfos[idxR1] : null)
			);
		}

		/** dir:0上,1上x2,2右上,3右上x2,4右,5右x2,6右下,7右下x2 */
		bool isChain(int dir, int boneIdx, int depthIdx) {

			static bool checkDepth(Plain self, BoneInfo from, BoneInfo to, int depth) {
				if ( to==null || self.getCnvPrm(to).depth<=depth ) return false;
				return from==null || depth<self.getCnvPrm(from).sideChainDepth;
			}

			static bool checkProc(
				Plain self,
				BoneInfo from,
				BoneInfo to1, BoneInfo to2,
				int fromDepth, int depthOfs
			) {
				if (to1==null && to2==null) return false;
				if (to1!=null && !checkDepth(self, from, to1, fromDepth+depthOfs)) return false;
				if (to2!=null && !checkDepth(self, to1, to2, fromDepth+depthOfs*2)) return false;

				var fromM = from==null ? 0 : self.getCnvPrm(from).getM(fromDepth);
				var toM = to2==null
					? self.getCnvPrm(to1).getM(fromDepth+depthOfs)
					: self.getCnvPrm(to2).getM(fromDepth+depthOfs*2);

				return 0.00000001f < fromM || 0.00000001f < toM;
			}

			var (bl1, bl0, bc, br0, br1) = getSideBoneInfos(boneIdx);

			switch (dir) {
			case 0: case 1:
				return checkProc(this, bc, br0, dir==1?br1:null, depthIdx, 0);
			case 2: case 3:
				return checkProc(this, bc, br0, dir==3?br1:null, depthIdx, 1);
			case 4: case 5:
				return checkProc(this, null, dir==5?null:bc, dir==5?bc:null, depthIdx, 1);
			case 6: case 7:
				return checkProc(this, bc, bl0, dir==3?bl1:null, depthIdx, 1);
			default:
				throw new ArgumentException("dir:" + dir);
			}
		}

	#if UNITY_EDITOR
		void OnValidate() {
			if (_boneInfos == null) return;
			foreach ( var i in _boneInfos ) {
				// Depthを有効範囲に丸める
				var cnvPrm = getCnvPrm(i);
				if ( i.boneTop == null ) {
					cnvPrm.depth = 0;
				} else {
					int depthMax = 1;
					for (var j=i.boneTop; j.childCount!=0; j=j.GetChild(0)) ++depthMax;

					cnvPrm.depth = Mathf.Clamp(cnvPrm.depth, 1, depthMax);
				}
			}
		}
			
		override protected void OnDrawGizmos() {
			base.OnDrawGizmos();
			if ( !UnityEditor.Selection.Contains( gameObject.GetInstanceID() ) ) return;

			if (_boneInfos == null) return;
			for (int i=0; i<_boneInfos.Length; ++i) {
				var b = _boneInfos[i];
				var cnvPrm = getCnvPrm(b);
				if (b.boneTop == null) continue;

				var trans = b.boneTop;
				for (int dCnt=0; dCnt!=cnvPrm.depth; ++dCnt) {
					Gizmos.color = new Color(1,1,1,0.5f);
					Gizmos.DrawSphere( trans.position, cnvPrm.getR(dCnt) );

					if (!Application.isPlaying) {
						Gizmos.color = new Color(1,1,0);
						if (dCnt != 0) Gizmos.DrawLine( trans.position, trans.parent.position );
						if (isChain(0,i,dCnt)) {
							var b2 = _boneInfos[(i+1)%_boneInfos.Length];
							var trans2 = b2.boneTop;
							for (int dCnt2=0; dCnt2!=dCnt; ++dCnt2) {
								if ( trans2.childCount==0 ) break; else trans2=trans2.GetChild(0);
							}
							Gizmos.DrawLine( trans.position, trans2.position );
						}
					}

					if ( trans.childCount==0 ) break; else trans=trans.GetChild(0);
				}
			}
		}
	#endif

		// --------------------------------------------------------------------------------------------
	}

}

