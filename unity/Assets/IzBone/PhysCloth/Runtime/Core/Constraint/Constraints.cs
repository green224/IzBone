using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using System.Collections.Generic;


namespace IzBone.PhysCloth.Core.Constraint {
	
	/** 複数の拘束条件をまとめて保持するコンテナ。Dotsから使用するのでStruct */
	public unsafe struct Constraints : IDisposable
	{
		public NativeArray<Distance>	distance;
		public NativeArray<MaxDistance>	maxDistance;
		public NativeArray<Axis>		axis;
		
		public int TotalLength =>
			distance.Length + maxDistance.Length + axis.Length;

		public Constraints(
			Controller.ConstraintMng[] src,
			NativeArray<Particle> points
		) {
			var d = new List<Distance>();
			var md = new List<MaxDistance>();
			var a = new List<Axis>();
			var pntsPtr = (Particle*)points.GetUnsafePtr();

			foreach (var i in src) {
				// 強度があまりにも弱い場合は拘束条件を追加しない
				if (Controller.ComplianceAttribute.LEFT_VAL*0.98f < i.compliance) continue;

				switch (i.mode) {
				case Controller.ConstraintMng.Mode.Distance:
					{// 距離拘束
						var b = new Distance{
							compliance = i.compliance,
							src = pntsPtr + i.srcPtclIdx,
							dst = pntsPtr + i.dstPtclIdx,
							defLen = i.param.x,
						};
						if ( b.isValid() ) d.Add( b );
					} break;
				case Controller.ConstraintMng.Mode.MaxDistance:
					{// 最大距離拘束
						var b = new MaxDistance{
							compliance = i.compliance,
							src = i.param.xyz,
							tgt = pntsPtr + i.srcPtclIdx,
							maxLen = i.param.w,
						};
						if ( b.isValid() ) md.Add( b );
					} break;
				case Controller.ConstraintMng.Mode.Axis:
					{// 稼働軸拘束
						var b = new Axis{
							compliance = i.compliance,
							src = pntsPtr + i.srcPtclIdx,
							dst = pntsPtr + i.dstPtclIdx,
							axis = i.param.xyz,
						};
						if ( b.isValid() ) a.Add( b );
					} break;
				default:throw new InvalidProgramException();
				}
			}

			distance = new NativeArray<Distance>( d.ToArray(), Allocator.Persistent );
			maxDistance = new NativeArray<MaxDistance>( md.ToArray(), Allocator.Persistent );
			axis = new NativeArray<Axis>( a.ToArray(), Allocator.Persistent );
		}

		/** 破棄する。最後に必ず呼ぶこと */
		public void Dispose() {
			if (distance.IsCreated) distance.Dispose();
			if (maxDistance.IsCreated) maxDistance.Dispose();
			if (axis.IsCreated) axis.Dispose();
		}

//		~Constraints() {
//			if (
//				distance.IsCreated ||
//				axis.IsCreated
//			) {
//				Debug.LogError("Constraints is not disposed");
//				Dispose();
//			}
//		}
	}

}
