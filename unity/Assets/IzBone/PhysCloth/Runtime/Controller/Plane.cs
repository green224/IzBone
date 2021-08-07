using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

using System.Collections.Generic;


namespace IzBone.PhysCloth.Controller {
using Common;
using Common.Field;

using RangeSC = Common.Field.SimpleCurveRangeAttribute;
using SC = Common.Field.SimpleCurve;

/**
 * IzBoneを使用するオブジェクトにつけるコンポーネント。
 * 平面的な布のようなものを再現する際に使用する
 */
[AddComponentMenu("IzBone/PhysCloth_Plane")]
public unsafe sealed class Plane : Base {
	// ------------------------------- inspectorに公開しているフィールド ------------------------

	/** ボーンをランタイム時バッファへの変換するときの情報 */
	[Serializable] internal sealed class ConversionParam {
		[JointCount(1)] public int depth = 1;
		[JointCount] public int fixCount = 1;
		[JointCount] public int excludeSideChain = 0;
		public int sideChainDepth => depth - excludeSideChain;
		[RangeSC(0)] public SC m = 1;
		[RangeSC(0)] public SC r = 1;
		[RangeSC(0,180)] public SC maxAngle = 60;

		public float getM(int idx) => idx<fixCount ? 0 : m.evaluate( idx2rate(idx) );
		public float getR(int idx) => r.evaluate( idx2rate(idx) );
		public float getMaxAgl(int idx) => maxAngle.evaluate( idx2rate(idx) );

		float idx2rate(int idx) =>
			depth-fixCount<=1 ? 0 : ( (idx-fixCount) / (depth-fixCount-1f) );
	}

	/** 骨の情報 */
	[Serializable] internal sealed class BoneInfo {
		[UnityEngine.Serialization.FormerlySerializedAs("boneTop")]
		public Transform endOfBone = null;

		// ローカルConversionParamを使用するか否か
		public bool useLocalCnvPrm = false;
		// ローカルConversionParam
		public ConversionParam cnvPrm = new ConversionParam();

		// 一番上のパーティクル
		[NonSerialized] public Point point = null;
	}

	[Space]
	[SerializeField] internal BoneInfo[] _boneInfos = null;

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

		// 質点リストを構築
		var points = new List<Point>();
		foreach ( var i in _boneInfos ) {
			var cnvPrm = getCnvPrm(i);
			var p = new Point(points.Count, i.endOfBone) {
				m = cnvPrm.getM(cnvPrm.depth-1),
				r = cnvPrm.getR(cnvPrm.depth-1),
				maxAngle = cnvPrm.getMaxAgl(cnvPrm.depth-1),
			};
			points.Add(p);

			int k = 1;
			for (var j=p.trans; k<cnvPrm.depth; ++k) {
				j=j.parent;
				var idx = cnvPrm.depth - 1 - k;
				var newP = new Point(points.Count, j) {
					child = p,
					m = cnvPrm.getM(idx),
					r = cnvPrm.getR(idx),
					maxAngle = cnvPrm.getMaxAgl(idx),
				};
				p.parent = newP;
				p = newP;
				points.Add(p);
			}

			i.point = p;
		}
		_points = points.ToArray();

		// 制約リストを構築
		_constraints.Clear();
		for (int i=0; i<_boneInfos.Length; ++i) {
			var (bl1, bl0, bc, br0, br1) = getSideBoneInfos(i);
			var c = bc.point;
			var l0 = bl0?.point; var l1 = bl1?.point;
			var r0 = br0?.point; var r1 = br1?.point;
			var d0 = c.child;    var d1 = d0?.child;
			var ld0 = l0?.child; var ld1 = l1?.child?.child;
			var rd0 = r0?.child; var rd1 = r1?.child?.child;

			int depth=0;
			while (c!=null) {
				if (d0 !=null && isChain(4,i,depth)) addCstr(_cmpl_direct, c, d0);
				if (d1 !=null && isChain(5,i,depth)) addCstr(_cmpl_bend,   c, d1);
				if (r0 !=null && isChain(0,i,depth)) addCstr(_cmpl_side,   c, r0);
				if (r1 !=null && isChain(1,i,depth)) addCstr(_cmpl_bend,   c, r1);
				if (rd0!=null && isChain(2,i,depth)) addCstr(_cmpl_diag,   c, rd0);
				if (rd1!=null && isChain(3,i,depth)) addCstr(_cmpl_bend,   c, rd1);
				if (ld0!=null && isChain(6,i,depth)) addCstr(_cmpl_diag,   c, ld0);
				if (ld1!=null && isChain(7,i,depth)) addCstr(_cmpl_bend,   c, ld1);

				c=c.child;
				r0=r0?.child;
				r1=r1?.child;
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
		coreUpdate(Time.deltaTime);
	}

	/** BoneInfoから、グローバルConversionParamを使用するか否かを考慮して、ConversionParamを取得する */
	internal ConversionParam getCnvPrm(BoneInfo boneInfo) =>
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
	internal bool isChain(int dir, int boneIdx, int depthIdx) {

		static bool checkDepth(Plane self, BoneInfo from, BoneInfo to, int depth) {
			if ( to==null || self.getCnvPrm(to).depth<=depth ) return false;
			return from==null || depth<self.getCnvPrm(from).sideChainDepth;
		}

		static bool checkProc(
			Plane self,
			BoneInfo from,
			BoneInfo to1, BoneInfo to2,
			int fromDepth, int depthOfs
		) {
			if (to1==null && to2==null) return false;
			if (to1!=null && !checkDepth(self, from, to1, fromDepth+depthOfs)) return false;
			if (to2!=null && !checkDepth(self, to1, to2, fromDepth+depthOfs*2)) return false;

			var fromIsFixed = from == null ? true
				: fromDepth < self.getCnvPrm(from).fixCount;
			var toIsFixed = to2 == null
				? fromDepth+depthOfs   < self.getCnvPrm(to1).fixCount
				: fromDepth+depthOfs*2 < self.getCnvPrm(to2).fixCount;

			return !fromIsFixed || !toIsFixed;
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
			return checkProc(this, bc, bl0, dir==7?bl1:null, depthIdx, 1);
		default:
			throw new ArgumentException("dir:" + dir);
		}
	}


	// --------------------------------------------------------------------------------------------
#if UNITY_EDITOR
	void OnValidate() {
		if (_boneInfos == null) return;
		foreach ( var i in _boneInfos ) {
			// Depthを有効範囲に丸める
			var cnvPrm = getCnvPrm(i);
			if ( i.endOfBone == null ) {
				cnvPrm.depth = 0;
			} else {
				int depthMax = 0;
				for (var j=i.endOfBone; j.parent!=null; j=j.parent) ++depthMax;

				cnvPrm.depth = Mathf.Clamp(cnvPrm.depth, 1, depthMax);
			}

			// fixCountを有効範囲に丸める
			cnvPrm.fixCount = clamp(cnvPrm.fixCount, 0, cnvPrm.depth);

			// excludeSideChainを有効範囲に丸める
			cnvPrm.excludeSideChain = clamp(cnvPrm.excludeSideChain, 0, cnvPrm.depth);
		}
	}
#endif
}

}

