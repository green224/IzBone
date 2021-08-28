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
		public ParticleMng parent, child, left, right;
		public float m;
		public float r;
		public float maxAngle;
		public float angleCompliance;
		public HalfLife restoreHL;		// 初期位置への復元半減期
		public float maxMovableRange;	// デフォルト位置からの移動可能距離

		public ParticleMng(int idx, Transform trans) {
			this.idx = idx;
			this.trans = trans;
			resetDefaultPose();
		}

		public void setParams(
			float m,
			float r,
			float maxAngle,
			float angleCompliance,
			HalfLife restoreHL,
			float maxMovableRange
		) {
			this.m = m;
			this.r = r;
			this.angleCompliance = angleCompliance;
			this.maxAngle = maxAngle;
			this.restoreHL = restoreHL;
			this.maxMovableRange = maxMovableRange;
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
		public enum Mode {
			Distance,			// 距離で拘束する
			MaxDistance,		// 指定距離未満になるように拘束する(最大距離を指定)
			Axis,				// 移動可能軸を指定して拘束する
		}
		public Mode mode;
		public int srcPtclIdx, dstPtclIdx;

		public float compliance;
		public float4 param;
	}

}
