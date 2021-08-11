using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysCloth.Controller {
	
	public sealed class Point {
		public readonly int idx;
		public readonly Transform trans;
		public quaternion defaultParentRot;		// 親の初期姿勢
		public float4x4 defaultL2P;				// 初期L2P行列
		public Point parent, child;
		public float m;
		public float r;
		public float maxAngle = 60;

		public Point(int idx, Transform trans) {
			this.idx = idx;
			this.trans = trans;
			resetDefaultPose();
		}
		public void resetDefaultPose() {
			defaultParentRot = trans.parent?.localRotation ?? default;
			defaultL2P = Unity.Mathematics.float4x4.TRS(
				trans.localPosition,
				trans.localRotation,
				trans.localScale
			);
		}
	}

	public sealed class Constraint {
		public enum Mode {Distance, Axis}
		public Mode mode;

		public int srcPointIdx, dstPointIdx;
		public float compliance;

		public float3 axis = float3(1,0,0);
	}

}
