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
			if (tgt._izColliders!=null) foreach (var i in tgt._izColliders)
				i.DEBUG_drawGizmos();
		}

		// コンストレイントを描画
		if ( Common.Windows.GizmoOptionsWindow.isShowConnections ) {
			Gizmos8.color = Gizmos8.Colors.BoneMovable;
			if (tgt._constraints != null) foreach (var i in tgt._constraints) {
				var p0 = tgt._world.DEBUG_getPos( i.srcPtclIdx );
				var p1 = tgt._world.DEBUG_getPos( i.dstPtclIdx );
				Gizmos8.drawLine( p0, p1 );
			}
		}

		// 質点を描画
		if (tgt._particles != null) foreach (var i in tgt._particles) {
			if (i.m < 0.000001f) continue;
			var p = tgt._world.DEBUG_getPos( i.idx );

			// TODO : ここ、矢印にする
			if ( Common.Windows.GizmoOptionsWindow.isShowPtclV ) {
				var v = tgt._world.DEBUG_getV( i.idx );
				Gizmos8.color = new Color(0,0,1);
				Gizmos8.drawLine( p, p+v*0.03f );
			}

			if ( Common.Windows.GizmoOptionsWindow.isShowPtclNml ) {
				var nml = tgt._world.DEBUG_getNml( i.idx );
				Gizmos8.color = new Color(1,0,0);
				Gizmos8.drawLine( p, p+nml*0.03f );
			}
		}
	}
}

}
