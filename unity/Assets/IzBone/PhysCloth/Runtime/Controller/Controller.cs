using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;


namespace IzBone.PhysCloth.Controller {
	using Common;
	using Common.Field;
	
	public sealed class ParticleMng {
		public readonly int idx;
		public readonly Transform trans;
		public quaternion defaultParentRot;		// 親の初期姿勢
		public float4x4 defaultL2P;				// 初期L2P行列
		public ParticleMng parent, child;
		public float m;
		public float r;
		public float maxAngle;
		public HalfLife restoreHL;		// 初期位置への復元半減期

		public ParticleMng(int idx, Transform trans) {
			this.idx = idx;
			this.trans = trans;
			resetDefaultPose();
		}

		public void setParams(
			float m,
			float r,
			float maxAngle,
			HalfLife restoreHL
		) {
			this.m = m;
			this.r = r;
			this.maxAngle = maxAngle;
			this.restoreHL = restoreHL;
		}

		public void resetDefaultPose() {
			defaultParentRot = trans.parent?.localRotation ?? default;
			defaultL2P = Unity.Mathematics.float4x4.TRS(
				trans.localPosition,
				trans.localRotation,
				trans.localScale
			);
		}
	}

	public sealed class ConstraintMng {
		public enum Mode {Distance, Axis}
		public Mode mode;
		public int srcPtclIdx, dstPtclIdx;

		public float compliance;
		public float3 param;
	}

}
