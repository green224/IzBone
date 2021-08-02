using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;

using System.Runtime.InteropServices;
using System.Linq;

namespace IzPhysBone.Collider {

	/** 複数のコライダをまとめて保持するコンテナ */
	public unsafe class Colliders : IDisposable
	{
	//TODO : これはJobからアクセスできる形にする
		public IzCollider[] srcList;

		public NativeArray<Collider_Sphere>		spheres;
		public NativeArray<Collider_Capsule>	capsules;
		public NativeArray<Collider_Box>		boxes;
		public NativeArray<Collider_Plane>		planes;

		public Colliders(IzCollider[] srcList) {
			this.srcList = srcList;
		}

		/** 破棄する。最後に必ず呼ぶこと */
		public void Dispose() {
			if (spheres.IsCreated) spheres.Dispose();
			if (capsules.IsCreated) capsules.Dispose();
			if (boxes.IsCreated) boxes.Dispose();
			if (planes.IsCreated) planes.Dispose();
		}

		/** 更新処理 */
		public void update() {

			foreach (var i in srcList) i.update_phase0();
			foreach (var i in srcList) i.update_phase1();

			// コライダー数を計算
			int cnt_s = 0;
			int cnt_c = 0;
			int cnt_b = 0;
			int cnt_p = 0;
			foreach (var i in srcList) {
				switch (i.mode) {
				case IzCollider.Mode.Sphere		: ++cnt_s; break;
				case IzCollider.Mode.Capsule	: ++cnt_c; break;
				case IzCollider.Mode.Box		: ++cnt_b; break;
				case IzCollider.Mode.Plane		: ++cnt_p; break;
				default : throw new InvalidProgramException();
				}
			}

			// コライダー数に応じて、バッファサイズを更新する
			static void resetBufferSize<T>(ref NativeArray<T> buf, int size) where T:struct {
				if ( (buf.IsCreated ? buf.Length : 0) == size ) return;
				if (buf.IsCreated) buf.Dispose();
				if (size != 0) buf = new NativeArray<T>(size, Allocator.Persistent);
			}
			resetBufferSize(ref spheres, cnt_s);
			resetBufferSize(ref capsules, cnt_c);
			resetBufferSize(ref boxes, cnt_b);
			resetBufferSize(ref planes, cnt_p);

			// IzColliderコンポーネントから情報を構築する
			int idx_s = -1;
			int idx_c = -1;
			int idx_b = -1;
			int idx_p = -1;
			foreach (var i in srcList) {
				var l2gMat = i.l2gMat;
				switch (i.mode) {
				case IzCollider.Mode.Sphere :
					spheres[++idx_s] = new Collider_Sphere() {
						pos = new Vector3(l2gMat.m03,l2gMat.m13,l2gMat.m23),
						r = i.l2gMatClmNorm.x * i.r.x
					};
					break;
				case IzCollider.Mode.Capsule :
					capsules[++idx_c] = new Collider_Capsule() {
						pos = new Vector3(l2gMat.m03,l2gMat.m13,l2gMat.m23),
						r_s = i.l2gMatClmNorm.x * i.r.x,
						r_h = i.l2gMatClmNorm.y * i.r.y,
						dir = new Vector3(l2gMat.m01, l2gMat.m11, l2gMat.m21) / i.l2gMatClmNorm.y
					};
					break;
				case IzCollider.Mode.Box :
					boxes[++idx_b] = new Collider_Box() {
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
					break;
				case IzCollider.Mode.Plane :
					planes[++idx_p] = new Collider_Plane() {
						pos = new Vector3(l2gMat.m03,l2gMat.m13,l2gMat.m23),
						dir = new Vector3(l2gMat.m02, l2gMat.m12, l2gMat.m22) / i.l2gMatClmNorm.z
					};
					break;
				default : throw new InvalidProgramException();
				}
			}
		}

		~Colliders() {
			if (
				spheres.IsCreated ||
				capsules.IsCreated ||
				boxes.IsCreated ||
				planes.IsCreated
			) {
				Debug.LogError("Colliders is not disposed");
				Dispose();
			}
		}
	}


	/** コライダのinterface */
	public unsafe interface ICollider {
		/** 指定の球がぶつかっていた場合、衝突しない位置まで引き離す */
		public bool solve(Collider_Sphere* s);
	}
	
	/** 球形コライダ [16bytes] */
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Collider_Sphere : ICollider {
		[FieldOffset(0)] public Vector3 pos;
		[FieldOffset(12)] public float r;

		/** 指定の球がぶつかっていた場合、衝突しない位置まで引き離す */
		public bool solve(Collider_Sphere* s) {
			var d = s->pos - pos;
			var dSqLen = d.sqrMagnitude;
			var sumR = r + s->r;
			if ( sumR*sumR < dSqLen ) return false;
			s->pos = pos + d * (sumR / Mathf.Sqrt(dSqLen+0.0000001f));
			return true;
		}
	}

	/** カプセル形状コライダ [32bytes] */
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Collider_Capsule : ICollider {
		[FieldOffset(0)] public Vector3 pos;	//!< 中央位置
		[FieldOffset(12)] public float r_s;		//!< 横方向の半径
		[FieldOffset(16)] public Vector3 dir;	//!< 縦方向の向き
		[FieldOffset(28)] public float r_h;		//!< 縦方向の長さ

		/** 指定の球がぶつかっていた場合、衝突しない位置まで引き離す */
		public bool solve(Collider_Sphere* s) {
			var d = s->pos - pos;

			// まずはバウンダリー球で衝突判定
			var dSqLen = d.sqrMagnitude;
			var sumR_s = r_s + s->r;
			var sumR_h = r_h + s->r;
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
			s->pos += d * (sumR_s/Mathf.Sqrt(dSqLen+0.0000001f) - 1);
			return true;
		}
	}

	/** 直方体コライダ[64bytes] */
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Collider_Box : ICollider {
		[FieldOffset(0)] public Vector3 pos;			//!< 中心位置
		[FieldOffset(12)] public Vector3 xAxis;			//!< X軸方向
		[FieldOffset(24)] public Vector3 yAxis;			//!< Y軸方向
		[FieldOffset(36)] public Vector3 zAxis;			//!< Z軸方向
		[FieldOffset(52)] public Vector3 r;				//!< 各ローカル軸方向の半径

		/** 指定の球がぶつかっていた場合、衝突しない位置まで引き離す */
		public bool solve(Collider_Sphere* s) {
			var d = s->pos - pos;
			var pr = s->r;

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
						s->pos += a.z*zAxis;
						return true;
					}
					// y方向に押し出し
					s->pos += a.y*yAxis;
					return true;
				}
				if (absEz < absEx) {
					// z方向に押し出し
					s->pos += a.z*zAxis;
					return true;
				}
				// x方向に押し出し
				s->pos += a.x*xAxis;
				return true;
			}
			
			// 頂点、辺、面で衝突の可能性
			if (xInner) a.x=0;
			if (yInner) a.y=0;
			if (zInner) a.z=0;
			var sqrA = a.sqrMagnitude;
			if ( pr*pr < sqrA ) return false;
			a *= pr/Mathf.Sqrt(sqrA+0.0000001f) - 1;
			s->pos += a.x*xAxis + a.y*yAxis + a.z*zAxis;
			return true;
		}
	}

	/** 無限平面コライダ [24bytes] */
	[StructLayout(LayoutKind.Explicit)]
	public unsafe struct Collider_Plane : ICollider {
		[FieldOffset(0)] public Vector3 pos;			//!< 平面上の位置
		[FieldOffset(12)] public Vector3 dir;			//!< 各ローカル軸方向の半径

		/** 指定の球がぶつかっていた場合、衝突しない位置まで引き離す */
		public bool solve(Collider_Sphere* s) {
			var d = s->pos - pos;

			var dLen = Vector3.Dot(d, dir);
			if (s->r < dLen) return false;

			s->pos += dir * (s->r - dLen);
			return true;
		}
	}


}


