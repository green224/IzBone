using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using System.Collections.Generic;


namespace IzBone.PhysCloth.Core {
	
	/** 拘束条件のinterface */
	public interface IConstraint {
		// 与えられたパラメータで拘束条件をする意味があるか否かをチェックする
		public bool isValid();
		// 拘束条件を解決する処理
		public float solve(float sqDt, float lambda);
	}

	/** 距離による拘束条件 */
	public unsafe struct Constraint_Distance : IConstraint {
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

	/**	最大距離による拘束条件。指定距離未満になるように拘束する */
	public unsafe struct Constraint_MaxDistance : IConstraint {
		public float3 src;
		public Particle* tgt;
		public float compliance;
		public float maxLen;

		public bool isValid() => MinimumM < tgt->invM;
		public float solve(float sqDt, float lambda) {

			// XPBDでの拘束条件の解決
			var at = compliance / sqDt;    // a~
			//   P = (x,y,z)
			// とすると、Distanceのときと同様に、
			//   Cj = |P| - d
			//   ∇Cj = P / |P|
			// また |P| < d のときは
			//   Cj = ∇Cj = 0
			var p = tgt->col.pos - src;
			var pLen = length(p);
			if ( pLen < maxLen ) return -lambda;

			var dlambda = (maxLen - pLen - at * lambda) / (tgt->invM + at);	// eq.18
			var correction = p * (dlambda / pLen);				// eq.17

			tgt->col.pos -= tgt->invM * correction;

			return dlambda;
		}

		const float MinimumM = 0.00000001f;
	}

	/** 指定方向最低距離による拘束条件 */
	public unsafe struct Constraint_MinDistN : IConstraint {
		public Particle* tgt;	// 押し出し対象
		public float3 src;		// 距離判定用の起点位置
		public float3 n;		// 押し出し方向
		public float minDist;	// 最小距離。srcからのn方向の距離がこれ未満の場合は押し出しを行う
		public float compliance;

		public bool isValid() => MinimumM < tgt->invM;
		public float solve(float sqDt, float lambda) {

			// XPBDでの拘束条件の解決
			var at = compliance / sqDt;    // a~
			//   P = (x,y,z)
			// とすると、
			//   P・n <  minDist  ならば  Cj = P・n - minDist
			//   P・n >= minDist  ならば  Cj = 0
			// であるので、
			//   P・n <  minDist  ならば  ∇Cj = n
			//   P・n >= minDist  ならば  ∇Cj = 0
			// また
			//   P・n <  minDist  ならば  ∇Cj・∇Cj = 1
			//   P・n >= minDist  ならば  ∇Cj・∇Cj = 0
			var cj = dot( tgt->col.pos - src, n ) - minDist;

			float dlambda;
			if (0 < cj) {
				dlambda = -lambda;		// eq.18

			} else {
				dlambda = (-cj - at * lambda) / (tgt->invM + at);	// eq.18
				var correction = dlambda * n;						// eq.17

				tgt->col.pos += tgt->invM * correction;
			}

			return dlambda;
		}

		const float MinimumM = 0.00000001f;
	}

	/** 可動軸方向による拘束条件 */
	public unsafe struct Constraint_Axis : IConstraint {
		public Particle* src;
		public Particle* dst;
		public float compliance;
		public float3 axis;

		public bool isValid() => MinimumM < src->invM + dst->invM;
		public float solve(float sqDt, float lambda) {
			var sumInvM = src->invM + dst->invM;

			// XPBDでの拘束条件の解決
			var at = compliance / sqDt;    // a~
			//   P = (x,y,z), A:固定軸
			// とすると、
			//   Cj = |P × A| = |B| , B:= P × A
			// であるので、計算すると
			//   ∇Cj = -( B × A ) / |B|
			var p = src->col.pos - dst->col.pos;
			var b = cross( p, axis );
			var bLen = length(b);
			var dCj = -cross(b, axis) / (bLen + 0.0000001f);

			var dlambda = (-bLen - at * lambda) / (dot(dCj,dCj)*sumInvM + at);	// eq.18
			var correction = dlambda * dCj;			// eq.17

			src->col.pos += +src->invM * correction;
			dst->col.pos += -dst->invM * correction;

			return dlambda;
		}

		const float MinimumM = 0.00000001f;
	}

	/** 角度制限による拘束条件 */
	public unsafe struct Constraint_Angle : IConstraint {
		public Particle* parent;
		public Particle* self;
		public Particle* child;
		public float compliance;
		public float3 defChildPos;	// 初期姿勢でのchildの位置

		public bool isValid() => MinimumM < parent->invM + self->invM + child->invM;
		public float solve(float sqDt, float lambda) {

			// XPBDでの拘束条件の解決
			var at = compliance / sqDt;    // a~

			// ここの細かい式の情報：https://qiita.com/green224/items/4c2afa3a2f9b2f4b3abd
			var u = normalizesafe( defChildPos - self->col.pos );
			var v = normalizesafe( cross( cross(u, child->col.pos - self->col.pos), u ) );
			var a2 = float2( dot(self->col.pos, u), dot(self->col.pos, v) );
			var b2 = float2( dot(child->col.pos, u), dot(child->col.pos, v) );
			var c2 = float2( dot(parent->col.pos, u), dot(parent->col.pos, v) );
			var s2 = float2( dot(defChildPos, u), dot(defChildPos, v) );
			var p = b2 - a2;
			var q = c2 - a2;
			var o = s2 - a2;
			var t = p / ( lengthsq(p) + 0.0000001f );
			var r = q / ( lengthsq(q) + 0.0000001f );

			var phiBase = atan2(q.y, q.x);
			var phi0 = atan2(o.y, o.x) - phiBase;
			var phi = atan2(p.y, p.x) - phiBase;

			var cj = phi - phi0;
			var nblACj = (t.y-r.y)*u + (r.x-t.x)*v;		// ∇_ACj
			var nblBCj =      -t.y*u + t.x*v;			// ∇_BCj
			var nblCCj =       r.y*u - r.x*v;			// ∇_CCj

			var dlambda =
				(-cj - at * lambda) / (
					lengthsq(nblACj)*self->invM +
					lengthsq(nblBCj)*child->invM +
					lengthsq(nblCCj)*parent->invM +
					at
				);									// eq.18

			self->col.pos   += self->invM   * dlambda * nblACj;			// eq.17
			child->col.pos  += child->invM  * dlambda * nblBCj;			// eq.17
			parent->col.pos += parent->invM * dlambda * nblCCj;			// eq.17

			return dlambda;
		}

		const float MinimumM = 0.00000001f;
	}

}
