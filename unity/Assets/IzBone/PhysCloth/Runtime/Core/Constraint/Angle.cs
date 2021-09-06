using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysCloth.Core.Constraint {
	
#if true
	/** 角度制限による拘束条件 */
	public unsafe struct Angle : IConstraint {
		public Particle* parent;
		public Particle* self;
		public Particle* child;
		public float compliance;
		public float3 defChildPos;	// 初期姿勢でのchildの位置

		public bool isValid() => MinimumM < parent->invM + self->invM + child->invM;
		public float solve(float sqDt, float lambda) {

			// XPBDでの拘束条件の解決
			var (cj,nblACj,nblBCj,nblCCj) = internalProcA();
			return internalProcB(sqDt,lambda,cj,nblACj,nblBCj,nblCCj);
		}

		// Constraint_AngleWithLimit からも同じ処理を行うので、共通化のために処理を分離しておく
		internal (
			float cj,
			float3 nblACj,
			float3 nblBCj,
			float3 nblCCj
		) internalProcA() {
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

			return (
				phi - phi0,
				(t.y-r.y)*u + (r.x-t.x)*v,	// ∇_ACj
				     -t.y*u + t.x*v,		// ∇_BCj
				      r.y*u - r.x*v			// ∇_CCj
			);
		}
		internal float internalProcB(
			float sqDt,
			float lambda,
			float cj,
			float3 nblACj,
			float3 nblBCj,
			float3 nblCCj
		) {
			var at = compliance / sqDt;    // a~
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

	/**
	 * 角度制限による拘束条件。
	 * 通常の角度拘束と、上限差分角度拘束を二つ同時に行うことで、計算負荷を下げたもの
	 */
	public unsafe struct AngleWithLimit {
		public Angle aglCstr;	// 通常の角度拘束
		public float compliance_nutral;		// 常にかかる角度拘束のコンプライアンス値
		public float compliance_limit;		// 制限角度を超えた際にかかる角度拘束のコンプライアンス値
		public float limitAngle;			// 制限角度。ラジアン角

		public (float lambda_nutral, float lambda_limit)
		solve(float sqDt, float lambda_nutral, float lambda_limit) {

			// XPBDでの拘束条件の解決
			var (cj,nblACj,nblBCj,nblCCj) = aglCstr.internalProcA();

			// 角度制限の上限に達しているか否かでコンプライアンス値とコンストレイント値を変更
			if (cj < -limitAngle || limitAngle < cj) {
				cj -= sign(cj) * limitAngle;
				aglCstr.compliance = compliance_limit;
				var dLambda = aglCstr.internalProcB(sqDt,lambda_limit,cj,nblACj,nblBCj,nblCCj);
				return (0, dLambda);
			} else {
				aglCstr.compliance = compliance_nutral;
				var dLambda = aglCstr.internalProcB(sqDt,lambda_nutral,cj,nblACj,nblBCj,nblCCj);
				return (dLambda, -lambda_limit);
			}
		}
	}

#else
	/** 角度制限による拘束条件 */
	public unsafe struct Angle : IConstraint {
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
#endif

}
