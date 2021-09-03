using System;
using UnityEngine;
using UnityEditor;

using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysCloth.Controller {
using Common;

abstract class BaseInspector : Editor
{
	override public void OnInspectorGUI() {

		// ギズモ表示用のボタンを表示
		using (new EditorGUILayout.HorizontalScope()) {
			EditorGUILayout.Space();
			if ( GUILayout.Button("Gizmos", GUILayout.Width(60)) ) {
				Common.Windows.GizmoOptionsWindow.open();
			}
		}

		base.OnInspectorGUI();
	}

	virtual protected void OnSceneGUI() {
		Gizmos8.drawMode = Gizmos8.DrawMode.Handle;
		var tgt = (Base)target;

		// 登録されているコライダを表示
		if ( Common.Windows.GizmoOptionsWindow.isShowCollider ) {
			if (tgt._izColliders!=null) foreach (var i in tgt._izColliders) {
				if (i == null) continue;
				i.DEBUG_drawGizmos();
			}
		}

		// コンストレイントを描画
		if ( Common.Windows.GizmoOptionsWindow.isShowConnections ) {
			Gizmos8.color = Gizmos8.Colors.BoneMovable;
			if (tgt._constraints != null) foreach (var i in tgt._constraints) {
				var p0 = tgt._world.DEBUG_getPtcl( i.srcPtclIdx ).col.pos;
				var p1 = tgt._world.DEBUG_getPtcl( i.dstPtclIdx ).col.pos;
				Gizmos8.drawLine( p0, p1 );
			}
		}

		// 質点を描画
		if (tgt._particles != null) foreach (var i in tgt._particles) {
			var ptcl = tgt._world.DEBUG_getPtcl( i.idx );
			var pos = ptcl.col.pos;
			var isFixed = i.m < 0.000001f;

			// パーティクル半径を描画
			if ( Common.Windows.GizmoOptionsWindow.isShowPtclR ) {
				Gizmos8.color = isFixed
					? Gizmos8.Colors.JointFixed
					: Gizmos8.Colors.JointMovable;

				var viewR = ptcl.col.r;
				if (isFixed) viewR = HandleUtility.GetHandleSize(pos)*0.1f;

				Gizmos8.drawSphere(pos, viewR);
			}

			// 移動可能距離を描画
			if ( Common.Windows.GizmoOptionsWindow.isShowLimitPos ) {
				if (!isFixed && 0 <= ptcl.maxMovableRange) {
					Gizmos8.color = Gizmos8.Colors.ShiftLimit;
					Gizmos8.drawWireCube(pos, Quaternion.identity, ptcl.maxMovableRange*2);
				}
			}

			// TODO : ここ、矢印にする
			if ( !isFixed && Common.Windows.GizmoOptionsWindow.isShowPtclV ) {
				var v = ptcl.v;
				Gizmos8.color = new Color(0,0,1);
				Gizmos8.drawLine( pos, pos+v*0.03f );
			}

//			if ( Common.Windows.GizmoOptionsWindow.isShowPtclNml ) {
//				var nml = ptcl.wNml;
//				Gizmos8.color = new Color(1,0,0);
//				Gizmos8.drawLine( pos, pos+nml*0.03f );
//			}

			// Fixedな親との接続を表示
			if (isFixed && ptcl.parentIdx != -1) {
				var a = tgt._particles[ptcl.parentIdx];
				if (a != null && a.m < 0.000001f) {
					var b = tgt._world.DEBUG_getPtcl(a.idx);

					Gizmos8.color = Gizmos8.Colors.BoneFixed;
					Gizmos8.drawLine( b.col.pos, ptcl.col.pos );
				}
			}
		}
	}
}

}
