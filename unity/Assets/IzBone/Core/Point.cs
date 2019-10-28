using System;
using UnityEngine;

using System.Runtime.InteropServices;
using System.Linq;

namespace IzBone.Core {
	
	/** シミュレート単位となるパーティクル1粒子分の情報 [32bytes] */
	[StructLayout(LayoutKind.Explicit)]
	public struct Point {
		[FieldOffset(0)] public Vector3 pos;
		[FieldOffset(12)] public Vector3 v;
		[FieldOffset(24)] public float invM;
		[FieldOffset(28)] public float r;

		public Point(Vector3 pos, float m, float r) {
			this.pos = pos;
			v = new Vector3(0,0,0);
			invM = m < MinimumM ? 0 : (1f/m);
			this.r = r;
		}

		const float MinimumM = 0.00000001f;
	}

}
