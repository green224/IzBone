using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzPhysBone.Cloth.Controller {
	
	public sealed class Point {
		public int idx;
		public Transform trans;
		public Point parent, child;
		public float m;
		public float r;
	}

	public sealed class Constraint {
		public enum Mode {Distance, Axis}
		public Mode mode;

		public int srcPointIdx, dstPointIdx;
		public float compliance;

		public Vector3 axis = new Vector3(1,0,0);
	}

}
