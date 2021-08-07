using System;
using UnityEngine;
using UnityEditor;

using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysCloth.Controller {
using Common;

abstract class BaseInspector : Editor
{
	void OnSceneGUI() {
		Gizmos8.drawMode = Gizmos8.DrawMode.Handle;
		var tgt = (Base)target;

		// 登録されているコライダを表示
		if (tgt._izColliders!=null) foreach (var i in tgt._izColliders)
			i.DEBUG_drawGizmos();

		// コンストレイントを描画
		Gizmos8.color = new Color(1,1,0);
		if (tgt._constraints != null) foreach (var i in tgt._constraints) {
			var p0 = tgt._world.DEBUG_getPos( i.srcPointIdx );
			var p1 = tgt._world.DEBUG_getPos( i.dstPointIdx );
			Gizmos8.drawLine( p0, p1 );
		}

		// 質点を描画
		// TODO : ここ、矢印にする
		Gizmos8.color = new Color(0,0,1);
		if (tgt._points != null) foreach (var i in tgt._points) {
			if (i.m < 0.000001f) continue;
			var v = tgt._world.DEBUG_getV( i.idx );
			var p = tgt._world.DEBUG_getPos( i.idx );
			Gizmos8.drawLine( p, p+v*0.03f );
		}
	}
}

}
