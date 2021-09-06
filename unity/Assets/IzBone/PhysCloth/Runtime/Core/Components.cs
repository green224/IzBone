
using UnityEngine;
using Unity.Entities;
using Unity.Mathematics;
using System.Runtime.InteropServices;

namespace IzBone.PhysCloth.Core {
	using Common;

	/** PhysCloth１セット分の、Particleの最親につけるコンポーネント */
	public struct OneCloth : IComponentData {
	}


//	// 以降、１コライダーごとのEntityに対して付けるコンポーネント群
//	public struct Body:IComponentData {}
//	public struct Body_Next:IComponentData {public Entity value;}	// 次のコライダーへの参照
//	public struct Body_ShapeType:IComponentData {public ShapeType value;}
//	public struct Body_Center:IComponentData {public float3 value;}
//	public struct Body_R:IComponentData {public float3 value;}
//	public struct Body_Rot:IComponentData {public quaternion value;}
//
//	[StructLayout(LayoutKind.Explicit)] public struct Body_RawCollider:IComponentData {
//		[FieldOffset(0)] public RawCollider.Sphere sphere;
//		[FieldOffset(0)] public RawCollider.Capsule capsule;
//		[FieldOffset(0)] public RawCollider.Box box;
//		[FieldOffset(0)] public RawCollider.Plane plane;
//
//		/***/ 指定の位置・半径・ShapeTypeで衝突を解決する *//*
//		public bool solveCollision(
//			ShapeType shapeType,
//			ref float3 pos,
//			float r
//		) {
//			var sc = new RawCollider.Sphere{pos=pos, r=r};
//
//			float3 n=0; float d=0; var isCol=false;
//			unsafe {
//				switch (shapeType) {
//				case ShapeType.Sphere  : isCol = sphere.solve(&sc,&n,&d); break;
//				case ShapeType.Capsule : isCol = capsule.solve(&sc,&n,&d); break;
//				case ShapeType.Box     : isCol = box.solve(&sc,&n,&d); break;
//				case ShapeType.Plane   : isCol = plane.solve(&sc,&n,&d); break;
//				}
//			}
//
//			if (isCol) pos += n * d;
//			return isCol;
//		}
//	}

	// ParticleとAuthoringとの橋渡し役を行うためのマネージドコンポーネント
	public sealed class Body_M2D:IComponentData {
		public Authoring.BaseAuthoring auth;				//!< 生成元
	}

}
