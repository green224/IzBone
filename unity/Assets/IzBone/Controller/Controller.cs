using System;
using UnityEngine;

using System.Runtime.InteropServices;

namespace IzBone.Controller {
	
	sealed class Point {
		public int idx;
		public Transform trans;
		public Point parent, child;
		public float m;
		public float r;
	}

	sealed class Constraint {
		public enum Mode {Distance}
		public Mode mode;
		public int srcPointIdx;
		public int dstPointIdx;
		public float compliance;
	}

}
