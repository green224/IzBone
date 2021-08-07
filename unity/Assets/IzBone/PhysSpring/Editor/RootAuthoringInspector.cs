using System;
using UnityEngine;
using UnityEditor;

using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysSpring {
using Common;

[CustomEditor(typeof(RootAuthoring))]
[CanEditMultipleObjects]
sealed class RootAuthoringInspector : Editor
{
	void OnSceneGUI() {
		Gizmos8.drawMode = Gizmos8.DrawMode.Handle;
		var tgt = (RootAuthoring)target;

		foreach (var bone in tgt._bones)
		foreach (var boneTgt in bone.targets) {

			var trns = boneTgt.endOfBone;
			if (trns == null) continue;

			// ƒWƒ‡ƒCƒ“ƒg‚²‚Æ‚É•`‰æ
			var posLst = new float3[bone.depth + 1];
			posLst[0] = trns.position;
			for (int i=0; i<bone.depth; ++i) {
				var next = trns.parent;
				posLst[i+1] = next.position;
				var iRate = (float)(bone.depth-1-i) / max(bone.depth-1, 1);

				// ƒp[ƒeƒBƒNƒ‹–{‘Ì‚ð•`‰æ
				Gizmos8.color = Gizmos8.Colors.JointMovable;
				Gizmos8.drawSphere(trns.position, bone.radius.evaluate(iRate));
				if (i == bone.depth-1) {
					Gizmos8.color = Gizmos8.Colors.JointFixed;
					var r = length(trns.position - next.position) * 0.1f;
					Gizmos8.drawSphere(next.position, r);
				}

				// ‚Â‚È‚ª‚è‚ð•`‰æ
				Gizmos8.color = Gizmos8.Colors.BoneMovable;
				Gizmos8.drawLine(next.position, trns.position);

				// Šp“x”ÍˆÍ‚ð•`‰æ
				var l2w = (float4x4)next.localToWorldMatrix;
				if (bone.rotShiftRate < 0.9999f) {
					var pos = l2w.c3.xyz;
					var rot = mul(
						Math8.fromToRotation(
							mul( next.rotation, float3(0,1,0) ),
							trns.position - next.position
						),
						next.rotation
					);
//					var scl = HandleUtility.GetHandleSize(l2w.c3.xyz)/2;
					var scl = length(trns.position - next.position)/2;
					Gizmos8.color = Gizmos8.Colors.AngleMargin;
					var agl = bone.angleMax.evaluate(iRate);
					Gizmos8.drawAngleCone( pos, rot, scl, agl );
					Gizmos8.color = Gizmos8.Colors.AngleLimit;
					agl *= 1 - bone.angleMargin.evaluate(iRate);
					Gizmos8.drawAngleCone( pos, rot, scl, agl );
				}

				// ˆÚ“®‰Â”\”ÍˆÍ‚ð•`‰æ
				if (0.00001f < bone.rotShiftRate) {
					var sft = bone.shiftMax.evaluate(iRate);
					var scl1 = Unity.Mathematics.float4x4.TRS(
						0, Unity.Mathematics.quaternion.identity, sft
					);
					sft *= 1 - bone.shiftMargin.evaluate(iRate);
					var scl0 = Unity.Mathematics.float4x4.TRS(
						0, Unity.Mathematics.quaternion.identity, sft
					);
					Gizmos8.color = Gizmos8.Colors.ShiftMargin;
					Gizmos8.drawWireCube( mul(l2w, scl1) );
					Gizmos8.color = Gizmos8.Colors.ShiftLimit;
					Gizmos8.drawWireCube( mul(l2w, scl0) );
				}

				trns = next;
			}

			// •`‰æ
//			Gizmos8.drawBones(posLst);
		}
	}
}

}
