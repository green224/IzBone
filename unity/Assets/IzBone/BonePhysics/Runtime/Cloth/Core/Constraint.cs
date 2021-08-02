using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using System.Runtime.InteropServices;
using System.Collections.Generic;
using System.Linq;

namespace IzBone.BonePhysics.Cloth.Core {
	
	/** 複数の拘束条件をまとめて保持するコンテナ */
	public unsafe class Constraints : IDisposable
	{
		public NativeArray<Constraint_Distance>	distance;
		public NativeArray<Constraint_Axis>		axis;
		
		public Constraints( List<Controller.Constraint> src, NativeArray<Point> points ) {
			var d = new List<Constraint_Distance>();
			var a = new List<Constraint_Axis>();
			var pntsPtr = (Point*)points.GetUnsafePtr();
			foreach (var i in src) {
				switch (i.mode) {
				case Controller.Constraint.Mode.Distance:
					{
						var b = new Constraint_Distance(){ compliance = i.compliance };
						b.reset(
							pntsPtr + i.srcPointIdx,
							pntsPtr + i.dstPointIdx
						);
						d.Add( b );
					} break;
				case Controller.Constraint.Mode.Axis:
					{
						var b = new Constraint_Axis(){ compliance = i.compliance, axis = i.axis };
						b.reset(
							pntsPtr + i.srcPointIdx,
							pntsPtr + i.dstPointIdx
						);
						a.Add( b );
					} break;
				default:throw new InvalidProgramException();
				}
			}
			distance = new NativeArray<Constraint_Distance>( d.ToArray(), Allocator.Persistent );
			axis = new NativeArray<Constraint_Axis>( a.ToArray(), Allocator.Persistent );
		}

		/** 破棄する。最後に必ず呼ぶこと */
		public void Dispose() {
			if (distance.IsCreated) distance.Dispose();
			if (axis.IsCreated) axis.Dispose();
		}

		~Constraints() {
			if (
				distance.IsCreated ||
				axis.IsCreated
			) {
				Debug.LogError("Constraints is not disposed");
				Dispose();
			}
		}
	}


	/** 拘束条件のinterface */
	public interface IConstraint {
		public float solve(float sqDt, float lambda);
	}

	/** 距離による拘束条件 [40bytes] */
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Constraint_Distance : IConstraint {
		[FieldOffset(0)] public Point* src;
		[FieldOffset(16)] public Point* dst;
		[FieldOffset(32)] public float compliance;
		[FieldOffset(36)] public float defLen;

		public void reset(Point* src, Point* dst) {
			this.src = src;
			this.dst = dst;
			defLen = length(src->col.pos - dst->col.pos);
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
			var p = src->col.pos - dst->col.pos;
			var pLen = length(p);

			var dlambda = (defLen - pLen - at * lambda) / (sumInvM + at);	// eq.18
			var correction = dlambda * p / (pLen + 0.0000001f);				// eq.17

			src->col.pos += +src->invM * correction;
			dst->col.pos += -dst->invM * correction;

			return dlambda;
		}

		const float MinimumM = 0.00000001f;
	}

	/** 可動軸方向による拘束条件 [48bytes] */
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Constraint_Axis : IConstraint {
		[FieldOffset(0)] public Point* src;
		[FieldOffset(16)] public Point* dst;
		[FieldOffset(32)] public float compliance;
		[FieldOffset(36)] public float3 axis;

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
