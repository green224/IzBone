using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;

using System.Collections.Generic;
using System.Linq;


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

	[Space]
	[SerializeField] internal Transform[] _topOfBones = null;

	[Space]
	// グローバルConversionParam
	[SerializeField] internal PhysParam _physParam = new PhysParam();
	[SerializeField] internal bool _isLoopConnect = false;		// スカートなどの筒状のつながりにする

	[Space]
	[Compliance][SerializeField] float _cmpl_direct = 0.000000001f;		//!< Compliance値 直接接続
	[Compliance][SerializeField] float _cmpl_side = 0.000000001f;		//!< Compliance値 横方向の接続
	[Compliance][SerializeField] float _cmpl_diag = 0.0000001f;			//!< Compliance値 捻じれ用の対角線接続


	// --------------------------------------- publicメンバ -------------------------------------

	// 対象のTopOfBonesを配列で得る
	public Transform[] TopOfBones => _topOfBones?.ToArray();


	// ----------------------------------- private/protected メンバ -------------------------------

	// 一番上のパーティクル
	ParticleMng _rootPtcl;

	/** ボーンの最大深度を得る */
	internal int Depth {get{
		int ret = 0;
		foreach (var i in _topOfBones) {
			if (i==null) continue;
			int depth = 0;
			for (var t = i;; t=t.GetChild(0), ++depth) {
				if (t.childCount == 0) break;
			}
			ret = max(ret, depth);
		}
		return ret;
	}}

	/** ParticlesとConstraintsのバッファをビルドする処理 */
	override protected void buildBuffers() {

		{// 質点リストを構築
			static ParticleMng genPtcl(
				int ptclIdx, Transform trans,
				ParticleMng parent, ParticleMng left
			) {
				var ret = new ParticleMng(ptclIdx, trans);
				if (parent != null) {
					ret.parent = parent;
					if (parent.child==null) parent.child = ret;
				}
				if (left != null) { ret.left = left; left.right = ret; }
				return ret;
			}

			var particles = new List<ParticleMng>();
			_rootPtcl = genPtcl(0, _topOfBones[0].parent, null, null);
			particles.Add( _rootPtcl );

			ParticleMng topPL = null;
			foreach ( var i in _topOfBones ) {

				var pL = topPL;
				var p = _rootPtcl;
				var trans = i;
				p = topPL = genPtcl( particles.Count, trans, p, pL );
				while (true) {
					particles.Add( p );
					if ( trans.childCount == 0 ) break;
					trans = trans.GetChild( 0 );
					pL = pL?.child;
					p = genPtcl( particles.Count, trans, p, pL );
				}
			}

			_particles = particles.ToArray();
		}

		// ループ接続の場合は、末端の質点を接続する
		if (_isLoopConnect) {
			var pL = _rootPtcl.child;
			var pR = pL; while (pR.right != null) pR = pR.right;

			while (pL!=null && pR!=null) {
				pR.right = pL; pL.left = pR;
				pL = pL.child; pR = pR.child;
			}
		}

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

		// 最大深度
		int depth = Depth;

		{// 質点パラメータを構築
			_rootPtcl.setParams(0,0,0,0,0,0);

			var pTop = _rootPtcl.child;
			for (var i=0; i<_topOfBones.Length; ++i, pTop=pTop.right) {
				int d = 0;
				for (var p=pTop; p!=null; p=p.child, ++d) {
					var bRate = PhysParam.idx2rate(d, depth, 1);
					p.setParams(
						_physParam.getM(bRate),
						_physParam.getR(bRate),
						_physParam.getMaxAgl(bRate),
						_physParam.getAglCompliance(bRate),
						_physParam.getRestoreHL(bRate),
						_physParam.getMaxMovableRange(bRate)
					);
				}
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
	void processInAllConstraint(Action<float, ParticleMng, ParticleMng> proc) {
		for (int i=1; i<_particles.Length; ++i) {	//i=0はルートなので除外
			var p = _particles[i];

			// 下と右
			if (p.child != null) proc(_cmpl_direct, p, p.child);
			if (p.right != null) proc(_cmpl_side, p, p.right);

			// 右斜め下
			if (p.right?.child != null)
				proc(_cmpl_diag, p, p.right?.child);
			else if (p.child?.right != null)
				proc(_cmpl_diag, p, p.child?.right);

			// 右斜め上
			if (p.right?.parent != null && p.right?.parent != _rootPtcl)
				proc(_cmpl_diag, p, p.right?.parent);
			else if (p.parent?.right != null && p.parent?.right != _rootPtcl)
				proc(_cmpl_diag, p, p.parent?.right);
		}
	}


	// --------------------------------------------------------------------------------------------
/*#if UNITY_EDITOR
	override protected void OnValidate() {
		base.OnValidate();

		if (_topOfBones == null) return;
		foreach ( var i in _topOfBones ) {
			// 親と子供が存在しない場合は正常じゃないので
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
#endif*/
}

}

