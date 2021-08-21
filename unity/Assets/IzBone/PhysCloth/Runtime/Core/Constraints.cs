using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

using System.Collections.Generic;


namespace IzBone.PhysCloth.Core {
	
	/** 複数の拘束条件をまとめて保持するコンテナ。Dotsから使用するのでStruct */
	public unsafe struct Constraints : IDisposable
	{
		public NativeArray<Constraint_Distance>	distance;
		public NativeArray<Constraint_Axis>		axis;
		
		public Constraints(
			Controller.ConstraintMng[] src,
			NativeArray<Particle> points
		) {
			var d = new List<Constraint_Distance>();
			var a = new List<Constraint_Axis>();
			var pntsPtr = (Particle*)points.GetUnsafePtr();

			foreach (var i in src) {
				// 強度があまりにも弱い場合は高速条件を追加しない
				if (Controller.ComplianceAttribute.LEFT_VAL*0.98f < i.compliance) continue;

				switch (i.mode) {
				case Controller.ConstraintMng.Mode.Distance:
					{// 距離制約
						var b = new Constraint_Distance();
						b.reset(
							i.compliance,
							pntsPtr + i.srcPtclIdx,
							pntsPtr + i.dstPtclIdx,
							i.param.x
						);
						d.Add( b );
					} break;
				case Controller.ConstraintMng.Mode.Axis:
					{// 稼働軸制約
						var b = new Constraint_Axis();
						b.reset(
							i.compliance,
							pntsPtr + i.srcPtclIdx,
							pntsPtr + i.dstPtclIdx,
							i.param
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
