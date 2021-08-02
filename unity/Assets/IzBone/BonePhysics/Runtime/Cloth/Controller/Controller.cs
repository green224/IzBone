using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.BonePhysics.Cloth.Controller {
	
	public sealed class Point {
		public readonly int idx;
		public readonly Transform trans;
		public readonly quaternion defaultParentRot;		// 親の初期姿勢
		public Point parent, child;
		public float m;
		public float r;
		public float maxAngle = 60;

		public Point(int idx, Transform trans) {
			this.idx = idx;
			this.trans = trans;
			defaultParentRot = trans.parent.localRotation;
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
