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
		public float solve(float sqDt, float lambda);
	}

	/** 距離による拘束条件 */
	public unsafe struct Constraint_Distance : IConstraint {
		public Particle* src;
		public Particle* dst;
		public float compliance;
		public float defLen;

		public void reset(float compliance, Particle* src, Particle* dst, float defLen) {
			this.compliance = compliance;
			this.src = src;
			this.dst = dst;
			this.defLen = defLen;
		}
		public float solve(float sqDt, float lambda) {
			var sumInvM = src->invM + dst->invM;
			if (sumInvM < MinimumM) return 0;

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
			var correction = dlambda * (p / (pLen+0.0000001f));				// eq.17

			src->col.pos += +src->invM * correction;
			dst->col.pos += -dst->invM * correction;

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

		public void reset(
			float compliance,
			float3 src,
			float3 n,
			Particle* tgt,
			float minDist
		) {
			this.compliance = compliance;
			this.src = src;
			this.n = n;
			this.tgt = tgt;
			this.minDist = minDist;
		}
		public float solve(float sqDt, float lambda) {

			// この高速条件はコライダの衝突解決用に使用される。
			// 衝突がない場合はdefaultで初期化されるので、
			// その場合はλを0に初期化する。
			// （正直に計算してもΔλが-λになるため同一）
			if (tgt == null) return -lambda;


			if (tgt->invM < MinimumM) return 0;

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

#if false
	/**
	 * コライダによる指定距離押し出し拘束条件。
	 * 処理的にはConstraint_MinDistNと同じだが、
	 * 毎フレーム生成するために、Constraint_MinDistNよりパラメータを工夫している
	 */
	public unsafe struct Constraint_ColliderPush : IConstraint {
		public bool isEnable;	// 押し出しが必要か否か
		public Particle* tgt;	// 押し出し対象
		public float3 amount;	// 押し出し量
		public float compliance;

		public void reset(
			bool isEnable,
			float compliance,
			Particle* tgt,
			float3 amount
		) {
			this.isEnable = isEnable;
			this.compliance = compliance;
			this.tgt = tgt;
			this.amount = amount;
		}
		public float solve(float sqDt, float lambda) {
			if (tgt->invM < MinimumM) return 0;

			// 押し出し不要なときは、Cj=0として処理
			if ( !isEnable ) return -lambda;

			// XPBDでの拘束条件の解決
			var at = compliance / sqDt;    // a~
			//   押し出し量 : P = (x,y,z)
			// とすると、
			//   Cj = |P|
			// であるので、
			//   ∇Cj = P / |P|
			// また
			//   ∇Cj・∇Cj = 1
			var cj = length( amount );

			var dlambda = (-cj - at * lambda) / (tgt->invM + at);	// eq.18
			var correction = dlambda * (amount / (cj+0.0000001f));	// eq.17

			tgt->col.pos += tgt->invM * correction;

			return dlambda;
		}

		const float MinimumM = 0.00000001f;
	}
#endif

	/** 可動軸方向による拘束条件 */
	public unsafe struct Constraint_Axis : IConstraint {
		public Particle* src;
		public Particle* dst;
		public float compliance;
		public float3 axis;

		public void reset(float compliance, Particle* src, Particle* dst, float3 axis) {
			this.compliance = compliance;
			this.src = src;
			this.dst = dst;
			this.axis = axis;
		}
		public float solve(float sqDt, float lambda) {
			var sumInvM = src->invM + dst->invM;
			if (sumInvM < MinimumM) return 0;

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

}
