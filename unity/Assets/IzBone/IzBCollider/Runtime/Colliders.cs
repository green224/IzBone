using System;
using UnityEngine;

using Unity.Mathematics;
using static Unity.Mathematics.math;
using Unity.Collections;


//TODO : これはもう古いので、ECS移行したら消す
namespace IzBone.IzBCollider {
	using RawCollider;

	/** 複数のコライダをまとめて保持するコンテナ */
	public unsafe class Colliders : IDisposable
	{
		public BodyAuthoring[] srcList;

		public NativeArray<Sphere>		spheres;
		public NativeArray<Capsule>	capsules;
		public NativeArray<Box>		boxes;
		public NativeArray<Plane>		planes;

		public Colliders(BodyAuthoring[] srcList) {
			this.srcList = srcList ?? new BodyAuthoring[0];
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
				case ShapeType.Sphere	: ++cnt_s; break;
				case ShapeType.Capsule	: ++cnt_c; break;
				case ShapeType.Box		: ++cnt_b; break;
				case ShapeType.Plane	: ++cnt_p; break;
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

			// BodyAuthoringコンポーネントから情報を構築する
			int idx_s = -1;
			int idx_c = -1;
			int idx_b = -1;
			int idx_p = -1;
			foreach (var i in srcList) {
				var l2wMtx = i.l2wMtx;
				switch (i.mode) {
				case ShapeType.Sphere :
					spheres[++idx_s] = new Sphere() {
						pos = l2wMtx.c3.xyz,
						r = i.l2wMtxClmNorm.x * i.r.x,
					};
					break;
				case ShapeType.Capsule :
					capsules[++idx_c] = new Capsule() {
						pos = l2wMtx.c3.xyz,
						r_s = i.l2wMtxClmNorm.x * i.r.x,
						r_h = i.l2wMtxClmNorm.y * i.r.y,
						dir = l2wMtx.c1.xyz / i.l2wMtxClmNorm.y,
					};
					break;
				case ShapeType.Box :
					boxes[++idx_b] = new Box() {
						pos = l2wMtx.c3.xyz,
						xAxis = l2wMtx.c0.xyz / i.l2wMtxClmNorm.x,
						yAxis = l2wMtx.c1.xyz / i.l2wMtxClmNorm.y,
						zAxis = l2wMtx.c2.xyz / i.l2wMtxClmNorm.z,
						r = float3(
							i.l2wMtxClmNorm.x * i.r.x,
							i.l2wMtxClmNorm.y * i.r.y,
							i.l2wMtxClmNorm.z * i.r.z
						),
					};
					break;
				case ShapeType.Plane :
					planes[++idx_p] = new Plane() {
						pos = l2wMtx.c3.xyz,
						dir = l2wMtx.c2.xyz / i.l2wMtxClmNorm.z,
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


}


