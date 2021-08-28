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
		[RangeSC(0,1)] public SC aglRestorePow = 0;
		[RangeSC(0,1)] public SC restorePow = 0;
		public SC maxMovableRange = -1;

		public float getM(int idx) => idx<fixCount ? 0 : m.evaluate( idx2rate(idx) );
		public float getR(int idx) => r.evaluate( idx2rate(idx) );
		public float getMaxAgl(int idx) => maxAngle.evaluate( idx2rate(idx) );
		public float getAglCompliance(int idx) => ComplianceAttribute.showValue2Compliance(
			aglRestorePow.evaluate( idx2rate(idx) ) * 0.2f
		);
		public float getRestoreHL(int idx) => HalfLifeDragAttribute.showValue2HalfLife(
			restorePow.evaluate( idx2rate(idx) )
		);
		public float getMaxMovableRange(int idx) => maxMovableRange.evaluate( idx2rate(idx) );

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
		[NonSerialized] public ParticleMng particle = null;
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


	// --------------------------------------- publicメンバ -------------------------------------

	// ----------------------------------- private/protected メンバ -------------------------------

	// 接続方向
	internal enum ChainDir : int {
		Right=0, RightX2,	// boneIdx+1方向
		DownRight, DownRightX2,
		Down, DownX2,		// child方向
		DownLeft, DownLeftX2,
		Left, LeftX2,		// boneIdx-1方向
		UpLeft, UpLeftX2,
		Up, UpX2,			// parent方向
		UpRight, UpRightX2,
	}

	/** ParticlesとConstraintsのバッファをビルドする処理 */
	override protected void buildBuffers() {
		// 質点リストを構築
		var particles = new List<ParticleMng>();
		foreach ( var i in _boneInfos ) {
			var cnvPrm = getCnvPrm(i);
			var trans = i.endOfBone;

			// 1Chain分のParticleリストを作成
			ParticleMng p = null;
			var oneChain = new ParticleMng[cnvPrm.depth];
			for (int k = 0; k<cnvPrm.depth; ++k) {
				var idxInChain = cnvPrm.depth - 1 - k;
				var newP = new ParticleMng(particles.Count + idxInChain, trans);
				if (p != null) { newP.child = p; p.parent = newP; }
				oneChain[idxInChain] = p = newP;
				trans = trans.parent;
			}

			// paticlesにはparentから順番に入るようにする
			particles.AddRange(oneChain);

			i.particle = p;
		}
		_particles = particles.ToArray();

		// 質点の横の接続を構築
		processInAllChain( (dir, p0, p1) => {
			if (dir == ChainDir.Right) {
				p0.right = p1;
				p1.left = p0;
			}
		}, false );

		// 制約リストを構築
		var constraints = new List<ConstraintMng>();
		processInAllConstraint(
			(compliance, p0, p1) => {
				if (1 <= compliance) return;
				constraints.Add( new ConstraintMng() {
					mode = ConstraintMng.Mode.Distance,
					srcPtclIdx = p0.idx,
					dstPtclIdx = p1.idx,
					param = length(p0.trans.position - p1.trans.position),
				} );
			}
		);
		_constraints = constraints.ToArray();
	}

	/** ParticlesとConstraintsのパラメータを再構築する処理 */
	override protected void rebuildParameters() {

		// 質点パラメータを構築
		int idx = -1;
		foreach ( var i in _boneInfos ) {
			var cnvPrm = getCnvPrm(i);
			for (int k = 0; k<cnvPrm.depth; ++k) {
				_particles[++idx].setParams(
					cnvPrm.getM(k),
					cnvPrm.getR(k),
					cnvPrm.getMaxAgl(k),
					cnvPrm.getAglCompliance(k),
					cnvPrm.getRestoreHL(k),
					cnvPrm.getMaxMovableRange(k)
				);
			}
		}

		{// 制約パラメータを構築
			int i = -1;
			processInAllConstraint(
				(compliance, p0, p1) => {
					if (1 <= compliance) return;
					var c = _constraints[++i];
					c.compliance = compliance;
				}
			);
		}
	}

	/** 全ボーンに対して、ConstraintMng羅列する処理 */
	void processInAllConstraint(Action<float, ParticleMng, ParticleMng> proc) =>
		processInAllChain((dir, p0, p1) => {
			switch (dir) {
				case ChainDir.Right:		proc(_cmpl_side, p0, p1); break;
//				case ChainDir.RightX2:		proc(_cmpl_bend, p0, p1); break;
				case ChainDir.DownRight:	proc(_cmpl_diag, p0, p1); break;
//				case ChainDir.DownRightX2:	proc(_cmpl_bend, p0, p1); break;
				case ChainDir.Down:			proc(_cmpl_direct, p0, p1); break;
//				case ChainDir.DownX2:		proc(_cmpl_bend, p0, p1); break;
				case ChainDir.DownLeft:		proc(_cmpl_diag, p0, p1); break;
//				case ChainDir.DownLeftX2:	proc(_cmpl_bend, p0, p1); break;
			}
		}, true);

	/** 全ボーンに対して、接続を羅列する処理 */
	void processInAllChain(Action<ChainDir, ParticleMng, ParticleMng> proc, bool ignoreFixed) {
		for (int i=0; i<_boneInfos.Length; ++i) {
			var (bl1, bl0, bc, br0, br1) = getSideBoneInfos(i);

			var c = bc.particle;
			var l0 = bl0?.particle; var l1 = bl1?.particle;
			var r0 = br0?.particle; var r1 = br1?.particle;

			int depth=0;
			while (c!=null) {
				var pms = new [] {
					r0, r1,
					r0?.child,	r1?.child?.child,
					c.child,	c.child?.child,
					l0?.child,	l1?.child?.child,
					l0, l1,
					l0?.parent,	l1?.parent?.parent,
					c.parent,	c.parent?.parent,
					r0?.parent,	r1?.parent?.parent,
				};

				for (int dir=0; dir<pms.Length; ++dir) {
					var tgt = pms[dir];
					if ( tgt!=null && isChain((ChainDir)dir, i, depth, ignoreFixed) )
						proc( (ChainDir)dir, c, tgt );
				}

				c = c.child;
				l0 = l0?.child; l1 = l1?.child;
				r0 = r0?.child; r1 = r1?.child;
				++depth;
			}
		}
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

	/** 指定位置のパーティクル同士が接続しているか否かをチェックする */
	internal bool isChain(ChainDir dir, int boneIdx, int depthIdx, bool ignoreFixed) {

		static bool checkDepth(
			Plane self, BoneInfo from, BoneInfo to,
			int depth, int depthOfs, bool reverse
		) {
			if (reverse)	(from, to) = (to, from);
			else			depth += depthOfs;
			if ( to==null || self.getCnvPrm(to).depth<=depth ) return false;
			return from==null || depth<self.getCnvPrm(from).sideChainDepth;
		}

		static bool checkIsFixed(
			Plane self,
			BoneInfo from, BoneInfo to,
			int fromDepth, int toDepth
		) {
			var fromIsFixed = from==null || fromDepth < self.getCnvPrm(from).fixCount;
			var toIsFixed   = to  ==null || toDepth   < self.getCnvPrm(to).fixCount;
			return fromIsFixed && toIsFixed;
		}

		static bool checkProc(
			Plane self,
			BoneInfo from,
			BoneInfo to1, BoneInfo to2,
			int fromDepth, int depthOfs,
			bool reverse,
			bool ignoreFixed
		) {
			if (to1==null && to2==null) return false;
			if (to1!=null && !checkDepth(self, from, to1, fromDepth, depthOfs, reverse)) return false;
			if (to2!=null && !checkDepth(self, to1, to2, fromDepth, depthOfs*2, reverse)) return false;

			return !ignoreFixed || !(
				to2 == null
				? checkIsFixed(self, from, to1, fromDepth, fromDepth+depthOfs)
				: checkIsFixed(self, from, to2, fromDepth, fromDepth+depthOfs*2)
			);
		}

		static bool checkProcDirect(
			Plane self,
			BoneInfo from,
			int fromDepth, int depthOfs,
			bool ignoreFixed
		) {
			if (from==null) return false;
			if (!checkDepth(self, null, from, fromDepth, depthOfs, false)) return false;

			return !ignoreFixed ||
				!checkIsFixed(self, from, from, fromDepth, fromDepth+depthOfs);
		}

		var (bl1, bl0, bc, br0, br1) = getSideBoneInfos(boneIdx);

		int dirIdx = (int)dir/2;
		var dirIsX2 = (int)dir%2 == 1;
		var isReverse = (int)ChainDir.Left <= (int)dir;
		return dirIdx switch {
			0 => checkProc(this, bc, br0, dirIsX2?br1:null, depthIdx, 0, isReverse, ignoreFixed),
			1 => checkProc(this, bc, br0, dirIsX2?br1:null, depthIdx, 1, isReverse, ignoreFixed),
			2 => checkProcDirect(this, bc, depthIdx, dirIsX2?2:1, ignoreFixed),
			3 => checkProc(this, bc, bl0, dirIsX2?bl1:null, depthIdx, 1, isReverse, ignoreFixed),
			4 => checkProc(this, bc, bl0, dirIsX2?bl1:null, depthIdx, 0, isReverse, ignoreFixed),
			5 => checkProc(this, bc, bl0, dirIsX2?bl1:null, depthIdx, -1, isReverse, ignoreFixed),
			6 => checkProcDirect(this, bc, depthIdx, dirIsX2?-2:-1, ignoreFixed),
			7 => checkProc(this, bc, br0, dirIsX2?br1:null, depthIdx, -1, isReverse, ignoreFixed),
			_ => throw new ArgumentException("dir:" + dir)
		};
	}


	// --------------------------------------------------------------------------------------------
#if UNITY_EDITOR
	override protected void OnValidate() {
		base.OnValidate();

		if (_boneInfos == null) return;
		foreach ( var i in _boneInfos ) {
			// Depthを有効範囲に丸める
			var cnvPrm = getCnvPrm(i);
			if ( i.endOfBone == null ) {
				cnvPrm.depth = 0;
			} else {
				int depthMax = 0;
				for (var j=i.endOfBone; j.parent!=null; j=j.parent) ++depthMax;

				cnvPrm.depth = clamp(cnvPrm.depth, 1, depthMax);
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

