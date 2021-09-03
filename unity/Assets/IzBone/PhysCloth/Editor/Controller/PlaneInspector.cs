using System;
using UnityEngine;
using UnityEditor;

using Unity.Mathematics;
using static Unity.Mathematics.math;
using System.Linq;


namespace IzBone.PhysCloth.Controller {
using Common;

[CustomEditor(typeof(Plane))]
[CanEditMultipleObjects]
sealed class PlaneInspector : BaseInspector
{

	override public void OnInspectorGUI() {
		base.OnInspectorGUI();
	}


	/** １オブジェクトに対するOnSceneGUI。基本的に派生先からはこれを拡張すればOK */
	override protected void OnSceneGUI() {
		base.OnSceneGUI();

		// プレイ中のギズモ表示はBaseのものでOK
		if (Application.isPlaying) return;

		Gizmos8.drawMode = Gizmos8.DrawMode.Handle;
		var tgt = (Plane)target;
		var pp = tgt._physParam;
		if (pp == null) return;

		var tob = tgt._topOfBones?.Where(i=>i!=null)?.ToArray();

		{// 正確なパラメータが指定されているか否かをチェック
			if (tob == null || tob.Length == 0) return;

			int errCnt = 0;
			var rootTrans = tob[0].parent;
			if (rootTrans == null) {
				errCnt = 1;
			} else foreach (var i in tob) {
				if (i.parent != rootTrans) {errCnt = 2; break;}
				if (i.childCount == 0) {errCnt = 3; break;}
			}

			static void showWarn(string msg) => EditorGUILayout.HelpBox(msg, MessageType.Error);
			switch (errCnt) {
			case 1: showWarn("TopOfBonesには共通の親Transformが必要です"); return;
			case 2: showWarn("TopOfBonesには共通の親Transformが必要です"); return;
			case 3: showWarn("TopOfBonesに1も子供が存在しないTransformが指定されています"); return;
			}
		}

		// パーティクル部分を描画する処理
		static void drawPtcl(Transform trans, bool isFixed, float r, float movRange)
		{
			// パーティクル半径を描画
			if ( Common.Windows.GizmoOptionsWindow.isShowPtclR ) {
				Gizmos8.color = isFixed
					? Gizmos8.Colors.JointFixed
					: Gizmos8.Colors.JointMovable;

				var viewR = r;
				if (isFixed) viewR = HandleUtility.GetHandleSize(trans.position)*0.1f;

				Gizmos8.drawSphere(trans.position, viewR);
			}

			// 移動可能距離を描画
			if ( Common.Windows.GizmoOptionsWindow.isShowLimitPos ) {
				if (!isFixed && 0 <= movRange) {
					Gizmos8.color = Gizmos8.Colors.ShiftLimit;
					Gizmos8.drawWireCube(trans.position, trans.rotation, movRange*2);
				}
			}
		}

		// パーティクルの接続を描画する処理
		static void drawConnection(Transform trans0, Transform trans1, bool isFixed) {
			if (
				trans0 != null && trans1 != null &&
				Common.Windows.GizmoOptionsWindow.isShowConnections
			) {
				Gizmos8.color = isFixed ? Gizmos8.Colors.BoneFixed : Gizmos8.Colors.BoneMovable;
				Gizmos8.drawLine( trans0.position, trans1.position );
			}
		}

		// ギズモを描画
		int depth = tgt.Depth;
		drawPtcl( tob[0].parent, true, 0, 0 );
		var tLst0 = tob.Select(i=>i.parent).ToArray();
		var tLst1 = tob.ToArray();
		for (int dIdx = 0;; ++dIdx) {

			var bRate = Base.PhysParam.idx2rate(dIdx, depth, 1);
			for (int i=0; i<tLst0.Length; ++i) {
				if (tLst1[i] == null) continue;

				drawPtcl( tLst1[i], dIdx==0, pp.getR(bRate), pp.getMaxMovableRange(bRate) );
				drawConnection(tLst0[i], tLst1[i], dIdx==0);

				Transform transL, transR;
				if (i == 0) transL = tgt._isLoopConnect ? tLst1[tLst1.Length-1] : null;
				else transL = tLst1[i-1];
				if (i == tLst1.Length-1) transR = tgt._isLoopConnect ? tLst1[0] : null;
				else transR = tLst1[i+1];

				drawConnection(transR, tLst1[i], dIdx==0);
				drawConnection(transL, tLst1[i], dIdx==0);
			}

			var isAllNull = true;
			for (int i=0; i<tLst0.Length; ++i) {
				tLst0[i] = tLst1[i];
				tLst1[i] = tLst1[i].childCount==0 ? null : tLst1[i].GetChild(0);
				if (tLst1[i] != null) isAllNull = false;
			}
			if (isAllNull) break;
		}

	}
}

}
