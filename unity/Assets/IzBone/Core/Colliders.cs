using System;
using UnityEngine;

using System.Runtime.InteropServices;
using System.Linq;

namespace IzBone.Core {

	/** 複数のコライダをまとめて保持するコンテナ */
	unsafe class Colliders {
		public const int Capacity_S = 128;
		public const int Capacity_C = 64;
		public const int Capacity_B = 32;
		public const int Capacity_P = 16;

		public PinnedArray<Collider_Sphere>		spheres  = new PinnedArray<Collider_Sphere>();
		public PinnedArray<Collider_Capsule>	capsules = new PinnedArray<Collider_Capsule>();
		public PinnedArray<Collider_Box>		boxes    = new PinnedArray<Collider_Box>();
		public PinnedArray<Collider_Plane>		planes   = new PinnedArray<Collider_Plane>();
		public Collider_Sphere* ptrEnd_s;
		public Collider_Capsule* ptrEnd_c;
		public Collider_Box* ptrEnd_b;
		public Collider_Plane* ptrEnd_p;

		/** 初期化する。最初に一度だけ呼ぶこと */
		public void setup() {
			spheres.reset( new Collider_Sphere[Capacity_S] );
			capsules.reset( new Collider_Capsule[Capacity_C] );
			boxes.reset( new Collider_Box[Capacity_B] );
			planes.reset( new Collider_Plane[Capacity_P] );
		}

		/** 破棄する。最後に必ず呼ぶこと */
		public void release() {
			spheres.reset( null );
			capsules.reset( null );
			boxes.reset( null );
			planes.reset( null );
		}

		/** 更新処理 */
		public void update( Controller.IzCollider[] mngColliders ) {

			// IzColliderコンポーネントから情報を構築する
			ptrEnd_s = spheres.ptr;
			ptrEnd_c = capsules.ptr;
			ptrEnd_b = boxes.ptr;
			ptrEnd_p = planes.ptr;
			var ptrMax_s = spheres.ptrEnd;
			var ptrMax_c = capsules.ptrEnd;
			var ptrMax_b = boxes.ptrEnd;
			var ptrMax_p = planes.ptrEnd;
			foreach (var i in mngColliders) {
				var l2gMat = i.l2gMat;
				if ( ptrEnd_s!=ptrMax_s && i.mode==Controller.IzCollider.Mode.Sphere ) {
					*(ptrEnd_s++) = new Collider_Sphere() {
						pos = new Vector3(l2gMat.m03,l2gMat.m13,l2gMat.m23),
						r = i.l2gMatClmNorm.x * i.r.x
					};
				} else if ( ptrEnd_c!=ptrMax_c && i.mode==Controller.IzCollider.Mode.Capsule ) {
					*(ptrEnd_c++) = new Collider_Capsule() {
						pos = new Vector3(l2gMat.m03,l2gMat.m13,l2gMat.m23),
						r_s = i.l2gMatClmNorm.x * i.r.x,
						r_h = i.l2gMatClmNorm.y * i.r.y,
						dir = new Vector3(l2gMat.m01, l2gMat.m11, l2gMat.m21) / i.l2gMatClmNorm.y
					};
				} else if ( ptrEnd_b!=ptrMax_b && i.mode==Controller.IzCollider.Mode.Box ) {
					*(ptrEnd_b++) = new Collider_Box() {
						pos = new Vector3(l2gMat.m03,l2gMat.m13,l2gMat.m23),
						xAxis = new Vector3(l2gMat.m00, l2gMat.m10, l2gMat.m20) / i.l2gMatClmNorm.x,
						yAxis = new Vector3(l2gMat.m01, l2gMat.m11, l2gMat.m21) / i.l2gMatClmNorm.y,
						zAxis = new Vector3(l2gMat.m02, l2gMat.m12, l2gMat.m22) / i.l2gMatClmNorm.z,
						r = new Vector3(
							i.l2gMatClmNorm.x * i.r.x,
							i.l2gMatClmNorm.y * i.r.y,
							i.l2gMatClmNorm.z * i.r.z
						)
					};
				} else if ( ptrEnd_p!=ptrMax_p && i.mode==Controller.IzCollider.Mode.Plane ) {
					*(ptrEnd_p++) = new Collider_Plane() {
						pos = new Vector3(l2gMat.m03,l2gMat.m13,l2gMat.m23),
						dir = new Vector3(l2gMat.m02, l2gMat.m12, l2gMat.m22) / i.l2gMatClmNorm.z
					};
				}
			}
		}
	}
	
	/** 球形コライダ [16bytes] */
	[StructLayout(LayoutKind.Explicit)]
	unsafe struct Collider_Sphere {
		[FieldOffset(0)] public Vector3 pos;
		[FieldOffset(12)] public float r;

		/** 指定のPointがぶつかっていた場合、衝突しない位置まで引き離す */
		public bool solve(Point* p) {
			var d = p->pos - pos;
			var dSqLen = d.sqrMagnitude;
			var sumR = r + p->r;
			if ( sumR*sumR < dSqLen ) return false;
			p->pos = pos + d * (sumR / Mathf.Sqrt(dSqLen+0.0000001f));
			return true;
		}
	}

	/** カプセル形状コライダ [32bytes] */
	[StructLayout(LayoutKind.Explicit)]
	unsafe struct Collider_Capsule {
		[FieldOffset(0)] public Vector3 pos;	//!< 中央位置
		[FieldOffset(12)] public float r_s;		//!< 横方向の半径
		[FieldOffset(16)] public Vector3 dir;	//!< 縦方向の向き
		[FieldOffset(28)] public float r_h;		//!< 縦方向の長さ

		/** 指定のPointがぶつかっていた場合、衝突しない位置まで引き離す */
		public bool solve(Point* p) {
			var d = p->pos - pos;

			// まずはバウンダリー球で衝突判定
			var dSqLen = d.sqrMagnitude;
			var sumR_s = r_s + p->r;
			var sumR_h = r_h + p->r;
			if (sumR_s*sumR_s < dSqLen && sumR_h*sumR_h < dSqLen) return false;

			// 縦方向の位置により距離を再計算
			var len_h = Vector3.Dot(d, dir);
//			if (len_h < -sumR_h || sumR_h < len_h) return false;		// バウンダリー球で判定する場合はこれはいらない
			if (len_h < -r_h+r_s)		d += dir * (r_h-r_s);			// 下側の球との衝突可能性がある場合
			else if (len_h < r_h-r_s)	d -= dir * len_h;				// 中央との衝突可能性がある場合
			else						d += dir * (r_s-r_h);			// 上側の球との衝突可能性がある場合

			// 押し出し処理
			dSqLen = d.sqrMagnitude;
			if ( sumR_s*sumR_s < dSqLen ) return false;
			p->pos += d * (sumR_s/Mathf.Sqrt(dSqLen+0.0000001f) - 1);
			return true;
		}
	}

	/** 直方体コライダ[64bytes] */
	[StructLayout(LayoutKind.Explicit)]
	unsafe struct Collider_Box {
		[FieldOffset(0)] public Vector3 pos;			//!< 中心位置
		[FieldOffset(12)] public Vector3 xAxis;			//!< X軸方向
		[FieldOffset(24)] public Vector3 yAxis;			//!< Y軸方向
		[FieldOffset(36)] public Vector3 zAxis;			//!< Z軸方向
		[FieldOffset(52)] public Vector3 r;				//!< 各ローカル軸方向の半径

		/** 指定のPointがぶつかっていた場合、衝突しない位置まで引き離す */
		public bool solve(Point* p) {
			var d = p->pos - pos;
			var pr = p->r;

			// まずはバウンダリー球で衝突判定
			var dSqLen = d.sqrMagnitude;
			var sumR_x = r.x + pr;
			var sumR_y = r.y + pr;
			var sumR_z = r.z + pr;
			if (sumR_x*sumR_x + sumR_y*sumR_y + sumR_z*sumR_z < dSqLen) return false;

			// 各軸ごとに、内側かどうか、境目からの距離を計算
			var a = new Vector3(
				Vector3.Dot(d, xAxis),
				Vector3.Dot(d, yAxis),
				Vector3.Dot(d, zAxis)
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
				var absEx = Mathf.Abs(a.x);
				var absEy = Mathf.Abs(a.y);
				var absEz = Mathf.Abs(a.z);
				if (absEy < absEx) {
					if (absEz < absEy) {
						// z方向に押し出し
						p->pos += a.z*zAxis;
						return true;
					}
					// y方向に押し出し
					p->pos += a.y*yAxis;
					return true;
				}
				if (absEz < absEx) {
					// z方向に押し出し
					p->pos += a.z*zAxis;
					return true;
				}
				// x方向に押し出し
				p->pos += a.x*xAxis;
				return true;
			}
			
			// 頂点、辺、面で衝突の可能性
			if (xInner) a.x=0;
			if (yInner) a.y=0;
			if (zInner) a.z=0;
			var sqrA = a.sqrMagnitude;
			if ( pr*pr < sqrA ) return false;
			a *= pr/Mathf.Sqrt(sqrA+0.0000001f) - 1;
			p->pos += a.x*xAxis + a.y*yAxis + a.z*zAxis;
			return true;
		}
	}

	/** 無限平面コライダ [24bytes] */
	[StructLayout(LayoutKind.Explicit)]
	unsafe struct Collider_Plane {
		[FieldOffset(0)] public Vector3 pos;			//!< 平面上の位置
		[FieldOffset(12)] public Vector3 dir;			//!< 各ローカル軸方向の半径

		/** 指定のPointがぶつかっていた場合、衝突しない位置まで引き離す */
		public bool solve(Point* p) {
			var d = p->pos - pos;

			var dLen = Vector3.Dot(d, dir);
			if (p->r < dLen) return false;

			p->pos += dir * (p->r - dLen);
			return true;
		}
	}


}


