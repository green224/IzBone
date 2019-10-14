using System;
using UnityEngine;

using System.Runtime.InteropServices;
using System.Linq;

namespace IzBone.Core {
	
	/** 距離による拘束条件 [24bytes] */
	[StructLayout(LayoutKind.Explicit)]
	unsafe struct Constraint_Distance {
		[FieldOffset(0)] public Point* src;
		[FieldOffset(8)] public Point* dst;
		[FieldOffset(16)] public float compliance;
		[FieldOffset(20)] public float defLen;

		public void reset(Point* src, Point* dst) {
			this.src = src;
			this.dst = dst;
			defLen = (src->pos - dst->pos).magnitude;
		}
		public float solve(float sqDt, float lambda) {
			var sumInvM = src->invM + dst->invM;
			if (sumInvM < MinimumM) return 0;

			// XPBDでの拘束条件の解決
			// 参考:
			//		https://ipsj.ixsq.nii.ac.jp/ej/index.php?active_action=repository_view_main_item_detail&page_id=13&block_id=8&item_id=183598&item_no=1
			//		https://github.com/nobuo-nakagawa/xpbd
			var dst2src = src->pos - dst->pos;
			var dist = dst2src.magnitude;

			var c = compliance / sqDt;    // a~
			// ∇Cj = (x,y,z) / √ x^2 + y^2 + z^2
			var dlambda = (defLen - dist - c * lambda) / (sumInvM + c);	// eq.18
			var correction = dlambda * dst2src / (dist + 0.0000001f);	// eq.17

			src->pos += +src->invM * correction;
			dst->pos += -dst->invM * correction;

			return dlambda;
		}

		const float MinimumM = 0.00000001f;
	}

}
