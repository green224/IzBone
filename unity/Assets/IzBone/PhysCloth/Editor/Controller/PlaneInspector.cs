using System;
using UnityEngine;
using UnityEditor;

using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysCloth.Controller {
using Common;

[CustomEditor(typeof(Plane))]
[CanEditMultipleObjects]
sealed class PlaneInspector : BaseInspector
{
	/** １オブジェクトに対するOnSceneGUI。基本的に派生先からはこれを拡張すればOK */
	override protected void OnSceneGUI() {
		base.OnSceneGUI();
		Gizmos8.drawMode = Gizmos8.DrawMode.Handle;
		var tgt = (Plane)target;

		if (tgt._boneInfos == null) return;
		for (int i=0; i<tgt._boneInfos.Length; ++i) {
			var b = tgt._boneInfos[i];
			var cnvPrm = tgt.getCnvPrm(b);
			if (b.endOfBone == null) continue;

			var trans = b.endOfBone;
			var lastBoneLen = length(trans.position - trans.parent.position);	// depthが1の時用に、ここには適当な値を入れておく
			for (int dCnt=0; dCnt!=cnvPrm.depth; ++dCnt) {
				var idx = cnvPrm.depth - 1 - dCnt;

				// パーティクルを描画
				var boneLen = length(trans.position - trans.parent.position);
				var isFixedJoint = idx < cnvPrm.fixCount;
				Gizmos8.color = isFixedJoint
					? Gizmos8.Colors.JointFixed
					: Gizmos8.Colors.JointMovable;
				var viewR = isFixedJoint
//					? min(boneLen, lastBoneLen) * 0.1f
					? lastBoneLen * 0.15f
					: cnvPrm.getR(idx);
				Gizmos8.drawSphere(trans.position, viewR);

				// Editor停止中はConstraintがまだ未生成なので、適当にチェインを描画
				if (!Application.isPlaying) {
					Gizmos8.color = Gizmos8.Colors.BoneMovable;
//					if (dCnt != cnvPrm.depth-1)
//						Gizmos8.drawLine( trans.position, trans.parent.position );

					static Transform getJointTrans(Plane tgt, int boneIdx, int depthIdx) {
						boneIdx = (boneIdx + tgt._boneInfos.Length) % tgt._boneInfos.Length;
						var bi = tgt._boneInfos[ boneIdx ];
						var cp = tgt.getCnvPrm(bi);
						var ret = bi.endOfBone;
						for (int dCnt2=0; dCnt2!=cp.depth-1-depthIdx; ++dCnt2) {
							if ( ret.parent==null ) break; else ret=ret.parent;
						}
						return ret;
					}

					if (tgt.isChain(0,i,idx)) {
						var trans2 = getJointTrans(tgt, i+1, idx);
						Gizmos8.drawLine( trans.position, trans2.position );
					}
					if (tgt.isChain(2,i,idx)) {
						var trans2 = getJointTrans(tgt, i+1, idx+1);
						Gizmos8.drawLine( trans.position, trans2.position );
					}
					if (tgt.isChain(4,i,idx)) {
						var trans2 = getJointTrans(tgt, i, idx+1);
						Gizmos8.drawLine( trans.position, trans2.position );
					}
					if (tgt.isChain(6,i,idx)) {
						var trans2 = getJointTrans(tgt, i-1, idx+1);
						Gizmos8.drawLine( trans.position, trans2.position );
					}
				}

				lastBoneLen = boneLen;
				if ( trans.parent==null ) break; else trans=trans.parent;
			}
		}
	}
}

}
