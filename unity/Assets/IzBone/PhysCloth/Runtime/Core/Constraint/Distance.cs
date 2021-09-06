﻿using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysCloth.Core.Constraint {
	
	/** 距離による拘束条件 */
	public unsafe struct Distance : IConstraint {
		public Particle* src;
		public Particle* dst;
		public float compliance;
		public float defLen;

		public bool isValid() => MinimumM < src->invM + dst->invM;
		public float solve(float sqDt, float lambda) {
			var sumInvM = src->invM + dst->invM;

			// XPBDでの拘束条件の解決
			// 参考:
			//		http://matthias-mueller-fischer.ch/publications/XPBD.pdf
			//		https://ipsj.ixsq.nii.ac.jp/ej/index.php?active_action=repository_view_main_item_detail&page_id=13&block_id=8&item_id=183598&item_no=1
			//		https://github.com/nobuo-nakagawa/xpbd
			var at = compliance / sqDt;    // a~
			//   P = (x,y,z)
			// とすると、
			//   Cj = |P| - d
			// であるので、
			//   ∇Cj = (x,y,z) / √ x^2 + y^2 + z^2  =  P / |P|
			// また
			//   ∇Cj・∇Cj = 1
			var p = src->col.pos - dst->col.pos;
			var pLen = length(p);

			var dlambda = (defLen - pLen - at * lambda) / (sumInvM + at);	// eq.18
			var correction = p * (dlambda / (pLen+0.0000001f));				// eq.17

			src->col.pos += +src->invM * correction;
			dst->col.pos += -dst->invM * correction;

			return dlambda;
		}

		const float MinimumM = 0.00000001f;
	}

}
