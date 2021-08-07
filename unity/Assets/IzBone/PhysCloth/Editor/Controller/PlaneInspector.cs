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
	void OnSceneGUI() {
//		base.OnSceneGUI();
		Gizmos8.drawMode = Gizmos8.DrawMode.Handle;
		var tgt = (Plane)target;

		if (tgt._boneInfos == null) return;
		for (int i=0; i<tgt._boneInfos.Length; ++i) {
			var b = tgt._boneInfos[i];
			var cnvPrm = tgt.getCnvPrm(b);
			if (b.boneTop == null) continue;

			var trans = b.boneTop;
			for (int dCnt=0; dCnt!=cnvPrm.depth; ++dCnt) {

				// パーティクルを描画
				Gizmos8.color = cnvPrm.getM(dCnt)<0.00001f
					? Gizmos8.Colors.JointFixed
					: Gizmos8.Colors.JointMovable;
				Gizmos8.drawSphere( trans.position, cnvPrm.getR(dCnt) );

				// Editor停止中はConstraintがまだ未生成なので、適当にチェインを描画
				if (!Application.isPlaying) {
					Gizmos8.color = Gizmos8.Colors.BoneMovable;
					if (dCnt != 0) Gizmos8.drawLine( trans.position, trans.parent.position );
					if (tgt.isChain(0,i,dCnt)) {
						var b2 = tgt._boneInfos[(i+1)%tgt._boneInfos.Length];
						var trans2 = b2.boneTop;
						for (int dCnt2=0; dCnt2!=dCnt; ++dCnt2) {
							if ( trans2.childCount==0 ) break; else trans2=trans2.GetChild(0);
						}
						Gizmos8.drawLine( trans.position, trans2.position );
					}
				}

				if ( trans.childCount==0 ) break; else trans=trans.GetChild(0);
			}
		}
	}
}

}
