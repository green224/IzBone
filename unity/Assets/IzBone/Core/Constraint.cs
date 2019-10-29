using System;
using UnityEngine;

using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;

namespace IzBone.Core {
	
	/** 複数の拘束条件をまとめて保持するコンテナ */
	public unsafe class Constraints {

		public PinnedArray<Constraint_Distance>	distance = new PinnedArray<Constraint_Distance>();
		public PinnedArray<Constraint_Axis>		axis     = new PinnedArray<Constraint_Axis>();
		
		/** 初期化する。最初に一度だけ呼ぶこと */
		public void reset( List<Controller.Constraint> src, PinnedArray<Point> points ) {
			var d = new List<Constraint_Distance>();
			var a = new List<Constraint_Axis>();
			foreach (var i in src) {
				switch (i.mode) {
				case Controller.Constraint.Mode.Distance:
					{
						var b = new Constraint_Distance(){ compliance = i.compliance };
						b.reset(
							points.ptr + i.srcPointIdx,
							points.ptr + i.dstPointIdx
						);
						d.Add( b );
					} break;
				case Controller.Constraint.Mode.Axis:
					{
						var b = new Constraint_Axis(){ compliance = i.compliance, axis = i.axis };
						b.reset(
							points.ptr + i.srcPointIdx,
							points.ptr + i.dstPointIdx
						);
						a.Add( b );
					} break;
				default:throw new InvalidProgramException();
				}
			}
			distance.reset( d.ToArray() );
			axis.reset( a.ToArray() );
		}

		/** 破棄する。最後に必ず呼ぶこと */
		public void release() {
			distance.reset( null );
			axis.reset( null );
		}
	}



	/** 距離による拘束条件 [40bytes] */
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Constraint_Distance {
		[FieldOffset(0)] public Point* src;
		[FieldOffset(16)] public Point* dst;
		[FieldOffset(32)] public float compliance;
		[FieldOffset(36)] public float defLen;

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
			//		http://matthias-mueller-fischer.ch/publications/XPBD.pdf
			//		https://ipsj.ixsq.nii.ac.jp/ej/index.php?active_action=repository_view_main_item_detail&page_id=13&block_id=8&item_id=183598&item_no=1
			//		https://github.com/nobuo-nakagawa/xpbd
			var at = compliance / sqDt;    // a~
			//   P = (x,y,z)
			// とすると、
			//   Cj = |P| - d
			// であるので、
			//   ∇Cj = (x,y,z) / √ x^2 + y^2 + z^2
			// また
			//   ∇Cj・∇Cj = 1
			var p = src->pos - dst->pos;
			var pLen = p.magnitude;

			var dlambda = (defLen - pLen - at * lambda) / (sumInvM + at);	// eq.18
			var correction = dlambda * p / (pLen + 0.0000001f);				// eq.17

			src->pos += +src->invM * correction;
			dst->pos += -dst->invM * correction;

			return dlambda;
		}

		const float MinimumM = 0.00000001f;
	}

	/** 可動軸方向による拘束条件 [48bytes] */
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Constraint_Axis {
		[FieldOffset(0)] public Point* src;
		[FieldOffset(16)] public Point* dst;
		[FieldOffset(32)] public float compliance;
		[FieldOffset(36)] public Vector3 axis;

		public void reset(Point* src, Point* dst) {
			this.src = src;
			this.dst = dst;
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
			var p = src->pos - dst->pos;
			var b = Vector3.Cross( p, axis );
			var bLen = b.magnitude;
			var dCj = -Vector3.Cross(b, axis) / (bLen + 0.0000001f);

			var dlambda = (-bLen - at * lambda) / (Vector3.Dot(dCj,dCj)*sumInvM + at);	// eq.18
			var correction = dlambda * dCj;			// eq.17

			src->pos += +src->invM * correction;
			dst->pos += -dst->invM * correction;

			return dlambda;
		}

		const float MinimumM = 0.00000001f;
	}

}
