using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;

// TODO : これをRawCollider名前空間へ移動する
namespace IzBone.IzBCollider {

	/** コライダのinterface */
	public unsafe interface ICollider {
		/** 指定の球がぶつかっていた場合、引き離しベクトルを得る */
		public bool solve(Collider_Sphere* s, float3* oColN, float* oColDepth);
	}

	/** 球形コライダ */
	public unsafe struct Collider_Sphere : ICollider {
		public float3 pos;
		public float r;

		/** 指定の球がぶつかっていた場合、引き離しベクトルを得る */
		public bool solve(Collider_Sphere* s, float3* oColN, float* oColDepth) {
			var d = s->pos - pos;
			var dSqLen = lengthsq(d);
			var sumR = r + s->r;
			if ( sumR*sumR < dSqLen ) return false;

			var dLen = sqrt(dSqLen);
			*oColN = d / (dLen + 0.0000001f);
			*oColDepth = sumR - dLen;
			
			return true;
		}
	}

	/** カプセル形状コライダ */
	public unsafe struct Collider_Capsule : ICollider {
		public float3 pos;		//!< 中央位置
		public float r_s;		//!< 横方向の半径
		public float3 dir;		//!< 縦方向の向き
		public float r_h;		//!< 縦方向の長さ

		/** 指定の球がぶつかっていた場合、引き離しベクトルを得る */
		public bool solve(Collider_Sphere* s, float3* oColN, float* oColDepth) {
			var d = s->pos - pos;

			// まずはバウンダリー球で衝突判定
			var dSqLen = lengthsq(d);
			var sumR_s = r_s + s->r;
			var sumR_h = r_h + s->r;
			if (sumR_s*sumR_s < dSqLen && sumR_h*sumR_h < dSqLen) return false;

			// 縦方向の位置により距離を再計算
			var len_h = dot(d, dir);
//			if (len_h < -sumR_h || sumR_h < len_h) return false;		// バウンダリー球で判定する場合はこれはいらない
			if (len_h < -r_h+r_s)		d += dir * (r_h-r_s);			// 下側の球との衝突可能性がある場合
			else if (len_h < r_h-r_s)	d -= dir * len_h;				// 中央との衝突可能性がある場合
			else						d += dir * (r_s-r_h);			// 上側の球との衝突可能性がある場合

			// 球vs球の衝突判定
			dSqLen = lengthsq(d);
			if ( sumR_s*sumR_s < dSqLen ) return false;

			var dLen = sqrt(dSqLen);
			*oColN = d / (dLen + 0.0000001f);
			*oColDepth = sumR_s - dLen;

			return true;
		}
	}

	/** 直方体コライダ */
	public unsafe struct Collider_Box : ICollider {
		public float3 pos;			//!< 中心位置
		public float3 xAxis;		//!< X軸方向
		public float3 yAxis;		//!< Y軸方向
		public float3 zAxis;		//!< Z軸方向
		public float3 r;			//!< 各ローカル軸方向の半径

		/** 指定の球がぶつかっていた場合、引き離しベクトルを得る */
		public bool solve(Collider_Sphere* s, float3* oColN, float* oColDepth) {
			var d = s->pos - pos;
			var pr = s->r;

			// まずはバウンダリー球で衝突判定
			var dSqLen = lengthsq(d);
			var sumR_x = r.x + pr;
			var sumR_y = r.y + pr;
			var sumR_z = r.z + pr;
			if (sumR_x*sumR_x + sumR_y*sumR_y + sumR_z*sumR_z < dSqLen) return false;

			// 各軸ごとに、内側かどうか、境目からの距離を計算
			var a = new float3(
				dot(d, xAxis),
				dot(d, yAxis),
				dot(d, zAxis)
			) + r;
			bool xInner = false, yInner = false, zInner = false;
			if (0 < a.x) { a.x -= r.x*2; xInner = a.x<0; }
			if (0 < a.y) { a.y -= r.y*2; yInner = a.y<0; }
			if (0 < a.z) { a.z -= r.z*2; zInner = a.z<0; }

			// 内側で衝突
			if (xInner && yInner && zInner) {
				// 押し出し量を計算
				a.x = (a.x < -r.x ? (-2*r.x-a.x - pr) : (-a.x + pr));
				a.y = (a.y < -r.y ? (-2*r.y-a.y - pr) : (-a.y + pr));
				a.z = (a.z < -r.z ? (-2*r.z-a.z - pr) : (-a.z + pr));

				// 最も押し出し量が小さい方向に押し出し
				var absEx = abs(a.x);
				var absEy = abs(a.y);
				var absEz = abs(a.z);
				if (absEy < absEx) {
					if (absEz < absEy) {
						// z方向に押し出し
						*oColN = a.z < 0 ? -zAxis : zAxis;
						*oColDepth = absEz;
						return true;
					}
					// y方向に押し出し
					*oColN = a.y < 0 ? -yAxis : yAxis;
					*oColDepth = absEy;
					return true;
				}
				if (absEz < absEx) {
					// z方向に押し出し
					*oColN = a.z < 0 ? -zAxis : zAxis;
					*oColDepth = absEz;
					return true;
				}
				// x方向に押し出し
				*oColN = a.x < 0 ? -xAxis : xAxis;
				*oColDepth = absEx;
				return true;
			}
			
			// 頂点、辺、面で衝突の可能性
			if (xInner) a.x=0;
			if (yInner) a.y=0;
			if (zInner) a.z=0;
			var sqA = lengthsq(a);
			if ( pr*pr < sqA ) return false;

			var aLen = sqrt(sqA);
			*oColN = (a.x*xAxis + a.y*yAxis + a.z*zAxis) / (aLen + 0.0000001f);
			*oColDepth = pr - aLen;

			return true;
		}
	}

	/** 無限平面コライダ */
	public unsafe struct Collider_Plane : ICollider {
		public float3 pos;		//!< 平面上の位置
		public float3 dir;		//!< 平面の表方向の向き

		/** 指定の球がぶつかっていた場合、引き離しベクトルを得る */
		public bool solve(Collider_Sphere* s, float3* oColN, float* oColDepth) {
			var d = s->pos - pos;

			var dLen = dot(d, dir);
			if (s->r < dLen) return false;

			*oColN = dir;
			*oColDepth = s->r - dLen;

			return true;
		}
	}



	/** ForcePeneCancel用のコライダ。特殊コライダなのでIColliderは継承できない:球に対応するもの */
//	public unsafe struct Collider_FPCPlane_FromSphere {
//		public float4x4 l2wCur;		//!< 現在のL2W
//		public float4x4 l2wDef;		//!< 初期L2W
//
//		/** 指定の球がぶつかっていた場合、衝突しない位置まで引き離す */
//		public bool solve(Collider_Sphere* s) {
//			var d = s->pos - pos;
//
//			var dLen = dot(d, dir);
//			if (s->r < dLen) return false;
//
//			s->pos += dir * (s->r - dLen);
//			return true;
//		}
//	}

}


